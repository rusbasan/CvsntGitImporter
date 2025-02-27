/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Information about a file in CVS.
/// </summary>
class FileInfo
{
    private readonly Dictionary<string, Revision> _revisionForTag = new Dictionary<string, Revision>();

    private readonly OneToManyDictionary<Revision, string> _tagsForRevision =
        new OneToManyDictionary<Revision, string>();

    private readonly Dictionary<string, Revision> _revisionForBranch = new Dictionary<string, Revision>();
    private readonly Dictionary<Revision, string> _branchForRevision = new Dictionary<Revision, string>();
    private readonly Dictionary<Revision, Commit> _commits = new Dictionary<Revision, Commit>();

    /// <summary>
    /// The file's name.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The name of the branch that the file was created on.
    /// </summary>
    public string BranchAddedOn = "MAIN";

    /// <summary>
    /// Gets a list of all a file's tags.
    /// </summary>
    public IEnumerable<string> AllTags
    {
        get { return _revisionForTag.Keys; }
    }

    /// <summary>
    /// Gets a list of all a file's branches.
    /// </summary>
    public IEnumerable<string> AllBranches
    {
        get { return _revisionForBranch.Keys; }
    }

    /// <summary>
    /// The keyword substitution flags associate with the file in CVSNT, which can indicate various things but
    /// most-usefully indicates if a file is binary or text.
    /// </summary>
    public String KeywordSubstitution { get; set; } = String.Empty;

    /// <summary>
    /// Whether the file should be treated as binary (so not change) or text (have line endings normalized).
    /// </summary>
    /// <remarks>
    /// Supposedly a keyword substitution of "kb" or "kB" indicates the file is binary, but in actual repos it seems to
    /// just have a "b", so just look for that, as none of the other known substitutions have a "b" in them.
    /// </remarks>
    public Boolean IsBinary => KeywordSubstitution.Contains('b') || KeywordSubstitution.Contains('B');

    public FileInfo(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Add a tag to the file.
    /// </summary>
    public void AddTag(string name, Revision revision)
    {
        if (revision.IsBranch)
            throw new ArgumentException(String.Format("Invalid tag revision: {0} is a branch tag revision", revision));

        _revisionForTag[name] = revision;
        _tagsForRevision.Add(revision, name);
    }

    /// <summary>
    /// Add a branch tag to the file. This is a pseudo revision that marks the revision that the branch
    /// starts at along with the branch "number" (since multiple branches can be made at a specific revision).
    /// E.g. revision 1.5.0.4 is a branch at revision 1.5 and its revisions will be 1.5.4.1, 1.5.4.2, etc.
    /// </summary>
    public void AddBranchTag(string name, Revision revision)
    {
        if (!revision.IsBranch)
            throw new ArgumentException(String.Format("Invalid branch tag revision: {0}", revision));

        _revisionForBranch[name] = revision;
        _branchForRevision[revision.BranchStem] = name;
    }

    /// <summary>
    /// Gets the branch that a revision is on.
    /// </summary>
    public string? GetBranch(Revision revision)
    {
        if (revision.Parts.Count() == 2)
        {
            return "MAIN";
        }
        else
        {
            var branchStem = revision.BranchStem;
            return _branchForRevision.TryGetValue(branchStem, out var branchTag) ? branchTag : null;
        }
    }

    /// <summary>
    /// Gets the branch for a revision.
    /// </summary>
    public IEnumerable<string> GetBranchesAtRevision(Revision revision)
    {
        foreach (var kvp in _branchForRevision)
        {
            if (kvp.Key.BranchStem.Equals(revision))
                yield return kvp.Value;
        }
    }

    /// <summary>
    /// Get the revision for the branchpoint for a branch.
    /// </summary>
    public Revision GetBranchpointForBranch(string branch)
    {
        if (_revisionForBranch.TryGetValue(branch, out var branchRevision))
            return branchRevision.GetBranchpoint();
        else
            return Revision.Empty;
    }

    /// <summary>
    /// Gets a list of tags applied to a revision.
    /// </summary>
    public IEnumerable<string> GetTagsForRevision(Revision revision)
    {
        return _tagsForRevision[revision];
    }

    /// <summary>
    /// Gets the revision for a tag.
    /// </summary>
    /// <returns>the revision that a tag is applied to, or Revision.Empty if the tag does not exist</returns>
    public Revision GetRevisionForTag(string tag)
    {
        if (_revisionForTag.TryGetValue(tag, out var revision))
            return revision;
        else
            return Revision.Empty;
    }

    /// <summary>
    /// Is a revision on a branch (or the branch's parent branch)
    /// </summary>
    public bool IsRevisionOnBranch(Revision revision, string branch)
    {
        if (branch == "MAIN")
            return revision.Parts.Count() == 2;

        if (_revisionForBranch.TryGetValue(branch, out var branchRevision))
            return (revision.Parts.Count() > 2 && branchRevision.BranchStem == revision.BranchStem) ||
                   revision.Precedes(branchRevision);
        else
            return false;
    }

    /// <summary>
    /// Add a commit that references this file.
    /// </summary>
    public void AddCommit(Commit commit, Revision r)
    {
        _commits.Add(r, commit);
    }

    /// <summary>
    /// Updates a commit that references this file.
    /// </summary>
    public void UpdateCommit(Commit commit, Revision r)
    {
        _commits[r] = commit;
    }

    /// <summary>
    /// Get a commit for a specific revision.
    /// </summary>
    /// <returns>the commit that created that revision or null if not found</returns>
    public Commit? GetCommit(Revision r)
    {
        return _commits.TryGetValue(r, out var commit) ? commit : null;
    }

    public override string ToString()
    {
        return Name;
    }
}