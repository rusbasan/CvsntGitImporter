/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Tracks the state of the repository allowing commits to be replayed.
/// </summary>
class RepositoryState
{
    private readonly Dictionary<string, RepositoryBranchState> _branches =
        new Dictionary<string, RepositoryBranchState>();

    private readonly FileCollection? _allFiles;
    private readonly bool _setupInitialBranchState;

    private RepositoryState(FileCollection? allFiles, bool setupInitialBranchState)
    {
        _allFiles = allFiles;
        _setupInitialBranchState = setupInitialBranchState;
    }

    /// <summary>
    /// Create an instance of RepositoryState that tracks the full state of each branch, i.e. each
    /// branch inherits all live files from its parent.
    /// </summary>
    public static RepositoryState CreateWithFullBranchState(FileCollection allFiles)
    {
        return new RepositoryState(allFiles, true);
    }

    /// <summary>
    /// Create an instance of RepositoryState that tracks only new files added on a branch.
    /// </summary>
    public static RepositoryState CreateWithBranchChangesOnly()
    {
        return new RepositoryState(null, false);
    }

    /// <summary>
    /// Gets the state for a branch.
    /// </summary>
    public RepositoryBranchState this[string branch]
    {
        get
        {
            if (_branches.TryGetValue(branch, out var state))
                return state;

            state = CreateBranchState(branch);
            _branches[branch] = state;
            return state;
        }
    }

    /// <summary>
    /// Apply a commit.
    /// </summary>
    public void Apply(Commit commit)
    {
        if (commit.Branch == null) return;

        var state = this[commit.Branch];
        state.Apply(commit);

        // find any file revisions that are branchpoints for branches and update the state of those branches
        var branches = commit
            .SelectMany(f => f.File.GetBranchesAtRevision(f.Revision))
            .Distinct()
            .Where(b => _branches.ContainsKey(b));

        foreach (var branch in branches)
        {
            var tempCommit = new Commit("");
            foreach (var fr in commit.Where(f => f.File.GetBranchesAtRevision(f.Revision).Contains(branch)))
                tempCommit.Add(fr);
            this[branch].Apply(tempCommit);
        }
    }

    private RepositoryBranchState CreateBranchState(string branch)
    {
        var state = new RepositoryBranchState(branch);

        if (_setupInitialBranchState)
        {
            foreach (var file in _allFiles ?? Enumerable.Empty<FileInfo>())
            {
                var branchpointRevision = file.GetBranchpointForBranch(branch);
                if (branchpointRevision == Revision.Empty)
                    continue;

                var sourceBranch = file.GetBranch(branchpointRevision);
                if (sourceBranch != null)
                {
                    var sourceBranchRevision = this[sourceBranch][file.Name];

                    if (sourceBranchRevision != Revision.Empty)
                        state.SetUnsafe(file.Name, branchpointRevision);
                }
            }
        }

        return state;
    }
}