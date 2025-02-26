/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Collection of streams of commits for each branch.
/// </summary>
class BranchStreamCollection
{
    private readonly Dictionary<string, Commit> _roots = new Dictionary<string, Commit>();
    private readonly Dictionary<string, Commit> _heads = new Dictionary<string, Commit>();

    private string _lastBranch;
    private Commit _lastBranchHead;
    private int _nextIndex = 1;

    public BranchStreamCollection(IEnumerable<Commit> commits, IDictionary<string, Commit> branchpoints)
    {
        foreach (var commit in commits)
        {
            if (commit.Branch == "MAIN" || branchpoints.ContainsKey(commit.Branch))
                AddCommit(commit);
        }

        // join branches to their branchpoints
        foreach (var kvp in _roots)
        {
            if (kvp.Key == "MAIN")
                continue;

            var branchpoint = branchpoints[kvp.Key];
            branchpoint.AddBranch(kvp.Value);
            kvp.Value.Predecessor = branchpoint;
        }
    }

    /// <summary>
    /// Get the head commit for a branch.
    /// </summary>
    /// <returns>the first commit on the branch, or null if the branch does not exist</returns>
    public Commit this[string branch]
    {
        get
        {
            Commit root;
            return _roots.TryGetValue(branch, out root) ? root : null;
        }
    }

    /// <summary>
    /// Get a list of the names of the branches contained in this collection.
    /// </summary>
    public IEnumerable<string> Branches
    {
        get { return _roots.Keys.ToList(); }
    }

    /// <summary>
    /// Get a list of the names of all branches, sorted with parent branches before their children.
    /// </summary>
    public IEnumerable<string> OrderedBranches
    {
        get
        {
            yield return "MAIN";
            foreach (var branch in EnumerateBranches(_roots["MAIN"]))
                yield return branch;
        }
    }

    /// <summary>
    /// Get the head (the last commit) for a branch.
    /// </summary>
    /// <returns>the last Commit for the branch or null if the branch does not exist</returns>
    public Commit Head(string branch)
    {
        Commit head;
        return _heads.TryGetValue(branch, out head) ? head : null;
    }

    /// <summary>
    /// Append a commit to a branch.
    /// </summary>
    public void AppendCommit(Commit commit)
    {
        AddCommit(commit);
    }

    /// <summary>
    /// Move a commit in the list to the position of another.
    /// </summary>
    /// <exception cref="ImportException">there was a problem moving the commit</exception>
    public void MoveCommit(Commit commitToMove, Commit commitToReplace)
    {
        // only support moving forwards at the moment
        if (commitToMove.Index > commitToReplace.Index)
            throw new NotSupportedException();
        else if (commitToMove.Index == commitToReplace.Index)
            return;

        // extricate the commit from the list
        if (commitToMove.Predecessor != null && commitToMove.Predecessor.Successor == commitToMove)
            commitToMove.Predecessor.Successor = commitToMove.Successor;
        if (commitToMove.Successor != null)
            commitToMove.Successor.Predecessor = commitToMove.Predecessor;

        for (Commit commit = commitToMove.Successor; commit != null; commit = commit.Successor)
        {
            // swap Index values so they remain in order
            var tmp = commitToMove.Index;
            commitToMove.Index = commit.Index;
            commit.Index = tmp;

            if (commit == commitToReplace)
            {
                var branch = commitToMove.Branch;
                if (_roots[branch] == commitToMove)
                {
                    _roots[branch] = commitToMove.Successor;

                    if (branch != "MAIN")
                    {
                        // need to adjust the branchpoint Commit on the parent branch too
                        var branchpoint = commitToMove.Predecessor;
                        if (!branchpoint.Branches.Contains(commitToMove))
                        {
                            throw new ImportFailedException(String.Format(
                                "Expected to find commit {0} as a branch from branchpoint {1}",
                                commitToMove.CommitId, branchpoint.CommitId));
                        }

                        branchpoint.ReplaceBranch(commitToMove, commitToMove.Successor);
                    }
                }

                if (commit.Successor != null)
                    commit.Successor.Predecessor = commitToMove;
                commitToMove.Successor = commit.Successor;
                commitToMove.Predecessor = commit;
                commit.Successor = commitToMove;

                if (_heads[branch] == commitToReplace)
                    _heads[branch] = commitToMove;

                return;
            }
        }

        // should never happen
        throw new ImportFailedException(String.Format(
            "Failed to find commit with index {0} moving forward from {1} on branch {2}",
            commitToReplace.Index, commitToMove.Index, commitToMove.Branch));
    }

    /// <summary>
    /// Verify the datastructure is consistent.
    /// </summary>
    /// <returns></returns>
    public bool Verify()
    {
        foreach (var branch in Branches)
        {
            var root = this[branch];
            if (branch == "MAIN" && root.Predecessor != null)
                return false;
            else if (branch != "MAIN" && (root.Predecessor == null || !root.Predecessor.Branches.Contains(root)))
                return false;

            var previous = root;
            for (var c = root.Successor; c != null; previous = c, c = c.Successor)
            {
                if (c.Predecessor != previous)
                    return false;
                else if (c.Index <= previous.Index)
                    return false;
            }
        }

        return true;
    }

    private void AddCommit(Commit commit)
    {
        Commit head;
        var branch = commit.Branch;

        if (branch == _lastBranch)
        {
            // optimisation - assume last commit was on the same branch
            head = _lastBranchHead;
            head.Successor = commit;
            commit.Predecessor = head;
        }
        else
        {
            if (_heads.TryGetValue(branch, out head))
            {
                head.Successor = commit;
                commit.Predecessor = head;
            }
            else
            {
                _roots.Add(branch, commit);
            }

            _lastBranch = commit.Branch;
        }

        commit.Index = _nextIndex++;
        _lastBranchHead = commit;
        _heads[branch] = commit;
    }

    private IEnumerable<string> EnumerateBranches(Commit root)
    {
        for (var c = root; c != null; c = c.Successor)
        {
            foreach (var branchroot in c.Branches)
            {
                yield return branchroot.Branch;
                foreach (var branch in EnumerateBranches(branchroot))
                    yield return branch;
            }
        }
    }
}