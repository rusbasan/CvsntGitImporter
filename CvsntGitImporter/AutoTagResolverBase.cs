/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Abstract base class for TagResolver and BranchResolver.
/// </summary>
abstract class AutoTagResolverBase : ITagResolver
{
    private readonly ILogger _log;
    private IList<Commit> _allCommits = [];
    private readonly FileCollection _allFiles;
    private readonly bool _branches;
    private readonly List<string> _unresolvedTags = new List<string>();
    private readonly bool _continueOnError;
    private readonly bool _noCommitReordering;

    protected AutoTagResolverBase(ILogger log, FileCollection allFiles, bool branches, bool continueOnError,
        bool noCommitReordering)
    {
        _log = log;
        _allFiles = allFiles;
        _branches = branches;
        this.PartialTagThreshold = Config.DefaultPartialTagThreshold;
        _continueOnError = continueOnError;
        _noCommitReordering = noCommitReordering;
    }

    /// <summary>
    /// The number of untagged files that are encountered before a tag/branch is declared as partial
    /// and is abandoned.
    /// </summary>
    public int PartialTagThreshold { get; set; }

    /// <summary>
    /// Gets a lookup that returns the resolved commits for each tag.
    /// </summary>
    public IDictionary<string, Commit> ResolvedTags { get; private set; } = new Dictionary<String, Commit>();

    /// <summary>
    /// Gets a list of any unresolved tags.
    /// </summary>
    public IEnumerable<string> UnresolvedTags
    {
        get { return _unresolvedTags; }
    }

    /// <summary>
    /// Gets the (possibly re-ordered) list of commits.
    /// </summary>
    public IEnumerable<Commit> Commits
    {
        get { return _allCommits; }
    }

    /// <summary>
    /// Get the tags or branches for a specific revision of a file.
    /// </summary>
    protected abstract IEnumerable<string> GetTagsForFileRevision(FileInfo file, Revision revision);

    /// <summary>
    /// Get the revision for a specific tag or branch.
    /// </summary>
    protected abstract Revision GetRevisionForTag(FileInfo file, string tag);


    /// <summary>
    /// Resolve tags and try and fix those that don't immediately resolve.
    /// </summary>
    /// <returns>true if all tags were resolved (potentially after being fixed), or false
    /// if fixing tags failed</returns>
    public virtual bool Resolve(IEnumerable<string> tags, IEnumerable<Commit> commits)
    {
        _allCommits = commits.ToListIfNeeded();
        SetCommitIndices(_allCommits);

        _unresolvedTags.Clear();
        this.ResolvedTags = new Dictionary<string, Commit>();

        foreach (var tag in tags)
        {
            _log.WriteLine("Resolve {0}", tag);

            using (_log.Indent())
            {
                Commit? commit = null;
                try
                {
                    commit = ResolveTag(tag);
                }
                catch (TagResolutionException tre)
                {
                    _log.WriteLine("{0}", tre.Message);
                }

                if (commit == null)
                {
                    _unresolvedTags.Add(tag);
                    _log.WriteLine("Unresolved");
                }
                else
                {
                    ResolvedTags[tag] = commit;
                    _log.WriteLine("Resolved: {0}", commit.ConciseFormat);
                }
            }
        }

        CheckCommitIndices(_allCommits);
        return _unresolvedTags.Count == 0;
    }

    private Commit? ResolveTag(string tag)
    {
        var state = RepositoryState.CreateWithFullBranchState(_allFiles);
        var moveRecord = new CommitMoveRecord(tag, _log);
        Commit? curCandidate = null;

        Queue<string> branchPath;
        var lastCandidate = FindLastCandidate(tag, out branchPath);
        if (lastCandidate == null)
        {
            _log.WriteLine("No commits");
            return null;
        }

        var relevantCommits = FilterCommits(branchPath);

        foreach (var commit in relevantCommits)
        {
            state.Apply(commit);

            if (IsCandidate(tag, commit))
                curCandidate = commit;

            if (curCandidate != null)
            {
                var cmp = CompareCommitToTag(state, commit, tag, out var filesToMove);

                if (cmp == CommitTagMatch.Ahead)
                {
                    // the commit has one or more files that are later
                    moveRecord.AddCommit(commit, filesToMove ?? []);
                }
                else if (cmp == CommitTagMatch.ExactMatch)
                {
                    break;
                }
            }

            if (curCandidate == lastCandidate)
                break;
        }

        // now check added/removed files
        _log.WriteLine("Candidate: {0}", curCandidate?.ConciseFormat ?? String.Empty);
        var candidateBranchState = GetBranchStateForCommit(curCandidate, relevantCommits);
        CheckAddedRemovedFiles(tag, candidateBranchState, relevantCommits, moveRecord, ref curCandidate);

        // perform any moves
        moveRecord.FinalCommit = curCandidate;
        if (moveRecord.Commits.Any())
        {
            if (_noCommitReordering == false)
            {
                moveRecord.Apply(_allCommits);
            }
            else
            {
                // Return null rather than trying to use the current candidate, as that can lead to a Git error since it may be
                // pointing to a commit that hasn't been put into the Git repo yet
                return null;
            }
        }

        CheckCommitIndices(_allCommits);
        return curCandidate;
    }

    private Commit? FindLastCandidate(string tag, out Queue<string> branchPath)
    {
        var candidateCommits = from c in _allCommits
            where IsCandidate(tag, c)
            select c;

        Commit? lastCandidate = null;
        string? lastBranch = null;
        branchPath = new Queue<string>();

        foreach (var c in candidateCommits)
        {
            lastCandidate = c;
            var cBranch = c.Branch;
            if (cBranch != null && cBranch != lastBranch)
            {
                if (branchPath.Contains(cBranch))
                {
                    throw new TagResolutionException(String.Format(
                        "Tag {0} does not have a clean branch path: {1}->{2} (last commit: {3})",
                        tag, String.Join("->", branchPath), cBranch, c.ConciseFormat));
                }

                branchPath.Enqueue(cBranch);
                lastBranch = cBranch;
            }
        }

        return lastCandidate;
    }

    private List<Commit> FilterCommits(Queue<string> branchPath)
    {
        var finalBranch = branchPath.Last();
        var currentBranch = branchPath.Dequeue();
        var filteredCommits = new List<Commit>();

        foreach (var commit in _allCommits)
        {
            if (commit.Branch != currentBranch)
            {
                if (branchPath.Count > 0 && commit.Branch == branchPath.Peek())
                    currentBranch = branchPath.Dequeue();
                else
                    continue;
            }

            if (commit.Any(r => r.File.IsRevisionOnBranch(r.Revision, finalBranch)))
                filteredCommits.Add(commit);
        }

        return filteredCommits;
    }

    private bool IsCandidate(string tag, Commit commit)
    {
        return commit.Any(r => GetTagsForFileRevision(r.File, r.Revision).Contains(tag));
    }

    private enum CommitTagMatch
    {
        /// <summary>
        /// The commit has at least one file whose revision precedes the revision at the tag
        /// (but none that exceed).
        /// </summary>
        Behind,

        /// <summary>
        /// The commit is an exact match for the tag. All live files are at the tag version.
        /// </summary>
        ExactMatch,

        /// <summary>
        /// The commit contains at least one file whose revision exceeds the revision at the tag.
        /// </summary>
        Ahead,
    }

    private CommitTagMatch CompareCommitToTag(RepositoryState state, Commit commit, string tag,
        out List<FileInfo>? filesAhead)
    {
        var branchState = commit.Branch != null ? state[commit.Branch] : null;
        var result = CommitTagMatch.ExactMatch;
        filesAhead = null;

        foreach (var fr in commit.Where(r => !r.IsDead))
        {
            var file = fr.File;
            var tagRevision = GetRevisionForTag(file, tag);

            // ignore commits on untagged files for now
            if (tagRevision == Revision.Empty)
                continue;

            var curStateRevision = branchState?[file.Name];

            if (curStateRevision == tagRevision)
            {
                // Precedes() below also returns true if the revisions match, so we block that here so an identical
                // revision isn't incorrectly marked as behind
            }
            else if (curStateRevision?.Precedes(tagRevision) == true)
                result = CommitTagMatch.Behind;
            else if (curStateRevision != null && tagRevision.Precedes(curStateRevision))
                AddAndCreateList(ref filesAhead, file);
        }

        result = (filesAhead == null) ? result : CommitTagMatch.Ahead;

        if (result == CommitTagMatch.ExactMatch)
        {
            // if no files are ahead in the commit, now check whether the whole tree is at the correct version
            foreach (var file in _allFiles)
            {
                if (GetRevisionForTag(file, tag) != branchState?[file.Name])
                {
                    result = CommitTagMatch.Behind;
                    break;
                }
            }
        }

        return result;
    }

    private RepositoryBranchState GetBranchStateForCommit(Commit? targetCommit, List<Commit> commits)
    {
        var state = RepositoryState.CreateWithFullBranchState(_allFiles);

        foreach (var commit in commits)
        {
            state.Apply(commit);
            if (commit == targetCommit && targetCommit.Branch != null)
                return state[targetCommit.Branch];
        }

        throw new InvalidOperationException("Did not find target commit in commit stream");
    }

    private void CheckAddedRemovedFiles(string tag, RepositoryBranchState candidateBranchState, List<Commit> commits,
        CommitMoveRecord moveRecord, ref Commit? candidate)
    {
        List<FileInfo>? missingFiles = null;
        List<FileInfo>? extraFiles = null;
        var liveFiles = new HashSet<string>(candidateBranchState.LiveFiles);

        foreach (var file in _allFiles)
        {
            if (GetRevisionForTag(file, tag) == Revision.Empty)
            {
                if (liveFiles.Contains(file.Name))
                {
                    AddAndCreateList(ref extraFiles, file);
                    _log.WriteLine("Extra:   {0}", file.Name);

                    if (extraFiles?.Count > PartialTagThreshold)
                    {
                        if (_continueOnError == false)
                        {
                            throw new TagResolutionException(String.Format("Tag {0} appears to be a partial tag", tag));
                        }
                        else
                        {
                            Console.WriteLine($"Tag {tag} appears to be a partial tag");
                        }
                    }
                }
            }
            else
            {
                if (!liveFiles.Contains(file.Name))
                {
                    AddAndCreateList(ref missingFiles, file);
                    _log.WriteLine("Missing: {0}", file.Name);
                }
            }
        }

        if (missingFiles != null && candidate != null)
            HandleMissingFiles(tag, commits, missingFiles, moveRecord, ref candidate);

        if (extraFiles != null && candidate != null)
            HandleExtraFiles(tag, commits, extraFiles, moveRecord, ref candidate);
    }

    protected virtual void HandleMissingFiles(string tag, List<Commit> commits, IEnumerable<FileInfo> files,
        CommitMoveRecord moveRecord, ref Commit candidate)
    {
        int candidateIndex = commits.IndexOfFromEnd(candidate);
        string? tagBranch = candidate.Branch;

        foreach (var file in files)
        {
            var tagRevision = GetRevisionForTag(file, tag);

            // search forwards for the file being added
            int addCommitIndex = -1;
            if (candidateIndex < commits.Count - 1)
            {
                addCommitIndex = FindCommitForwards(commits, candidateIndex + 1,
                    c => c.Any(r =>
                        r.File == file &&
                        r.Revision == tagRevision &&
                        (tagBranch != null && r.File.IsRevisionOnBranch(r.Revision, tagBranch))));
            }

            if (addCommitIndex >= 0)
            {
                // add any intermediate commits to the list of those that need moving
                for (int i = candidateIndex + 1; i < addCommitIndex; i++)
                    moveRecord.AddCommit(commits[i], file);
                candidate = commits[addCommitIndex];
            }
            else
            {
                // search backwards for the file being deleted
                int deleteCommitIndex = -1;
                if (candidateIndex > 0)
                {
                    deleteCommitIndex = FindCommitBackwards(commits, candidateIndex,
                        c => c.Any(r =>
                            r.File == file &&
                            r.IsDead &&
                            (tagBranch != null && r.File.IsRevisionOnBranch(r.Revision, tagBranch))));
                }

                if (deleteCommitIndex < 0)
                {
                    if (_continueOnError == false)
                    {
                        throw new TagResolutionException(String.Format(
                            "Tag {0}: file {1} is tagged but a commit for it could not be found", tag, file));
                    }
                    else
                    {
                        Console.WriteLine(String.Format(
                            "Tag {0}: file {1} is tagged but a commit for it could not be found", tag, file));
                    }
                }
                else
                {
                    moveRecord.AddCommit(commits[deleteCommitIndex], file);
                }
            }
        }
    }

    private void HandleExtraFiles(string tag, List<Commit> commits, IEnumerable<FileInfo> files,
        CommitMoveRecord moveRecord, ref Commit candidate)
    {
        int candidateIndex = commits.IndexOfFromEnd(candidate);
        string? tagBranch = candidate.Branch;

        foreach (var file in files)
        {
            var tagRevision = GetRevisionForTag(file, tag);

            // search backwards for the file being added
            int addCommitIndex = -1;
            if (candidateIndex > 0)
            {
                addCommitIndex = FindLastCommitBackwards(commits, candidateIndex - 1,
                    c => c.Any(r =>
                        r.File == file &&
                        !r.IsDead &&
                        tagBranch != null && r.File.IsRevisionOnBranch(r.Revision, tagBranch)));
            }

            // search forwards for the file being deleted
            int deleteCommitIndex = -1;
            if (candidateIndex < commits.Count - 1)
            {
                deleteCommitIndex = FindCommitForwards(commits, candidateIndex,
                    c => c.Any(r =>
                        r.File == file &&
                        r.IsDead &&
                        tagBranch != null && r.File.IsRevisionOnBranch(r.Revision, tagBranch)));
            }

            if (deleteCommitIndex < 0 && addCommitIndex < 0)
            {
                throw new TagResolutionException(String.Format(
                    "Tag {0}: file {1} is not tagged but a commit removing it could not be found", tag, file));
            }

            // pick the closest of the two
            int addDistance = (addCommitIndex < 0) ? int.MaxValue : candidateIndex - addCommitIndex;
            int deleteDistance = (deleteCommitIndex < 0) ? int.MaxValue : deleteCommitIndex - candidateIndex;

            if (addDistance <= deleteDistance)
            {
                moveRecord.AddCommit(commits[addCommitIndex], file);
                for (int i = addCommitIndex + 1; i <= candidateIndex; i++)
                {
                    if (commits[i].Any(r => r.File == file))
                        moveRecord.AddCommit(commits[i], file);
                }
            }
            else
            {
                candidate = commits[deleteCommitIndex];
            }
        }
    }

    private static int FindCommitForwards(List<Commit> commits, int startIndex, Predicate<Commit> match)
    {
        for (int i = startIndex; i < commits.Count; i++)
        {
            if (match(commits[i]))
                return i;
        }

        return -1;
    }

    private static int FindCommitBackwards(List<Commit> commits, int startIndex, Predicate<Commit> match)
    {
        for (int i = startIndex; i >= 0; i--)
        {
            if (match(commits[i]))
                return i;
        }

        return -1;
    }

    private static int FindLastCommitBackwards(List<Commit> commits, int startIndex, Predicate<Commit> match)
    {
        int result = -1;

        for (int i = startIndex; i >= 0; i--)
        {
            if (match(commits[i]))
                result = i;
        }

        return result;
    }

    private static void SetCommitIndices(IList<Commit> commits)
    {
        for (int i = 0; i < commits.Count; i++)
            commits[i].Index = i;
    }

    private static void AddAndCreateList<T>(ref List<T>? list, T item)
    {
        if (list == null)
            list = new List<T>() { item };
        else
            list.Add(item);
    }

    [Conditional("DEBUG")]
    private static void CheckCommitIndices(IList<Commit> commits)
    {
        for (int i = 0; i < commits.Count; i++)
        {
            if (commits[i].Index != i)
                Debug.Fail("Commit indices out of sync");
        }
    }
}