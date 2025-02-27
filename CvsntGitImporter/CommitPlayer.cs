/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Playback commits in an appropriate order for importing them.
/// </summary>
class CommitPlayer
{
    private readonly ILogger _log;
    private readonly BranchStreamCollection _streams;
    private readonly Dictionary<string, Commit> _branchHeads = new Dictionary<string, Commit>();
    private static readonly Commit EndMarker = new Commit("ENDMARKER") { Index = int.MaxValue };

    public CommitPlayer(ILogger log, BranchStreamCollection streams)
    {
        _log = log;
        _streams = streams;
    }

    /// <summary>
    /// Get the total number of commits.
    /// </summary>
    public int Count
    {
        get { return _streams.Branches.Select(b => CountCommits(_streams[b])).Sum(); }
    }

    /// <summary>
    /// Get the commits in an order in which they can be imported.
    /// </summary>
    public IEnumerable<Commit> Play()
    {
        foreach (var branch in _streams.Branches)
        {
            var branchHead = _streams[branch];
            if (branchHead != null) _branchHeads[branch] = branchHead;
        }

        // ensure first commit is the first commit from MAIN
        var mainHead = _streams["MAIN"];

        if (mainHead != null)
        {
            yield return mainHead;
            UpdateHead("MAIN", mainHead.Successor);

            Commit? nextCommit;
            while ((nextCommit = GetNextCommit()) != null)
            {
                // ensure that any branch we merge from is far enough along
                if (nextCommit.MergeFrom != null)
                {
                    foreach (var branchCommit in FastForwardBranch(nextCommit.MergeFrom))
                        yield return branchCommit;
                }

                yield return nextCommit;

                var nextCommitBranch = nextCommit.Branch;
                if (nextCommitBranch != null) UpdateHead(nextCommitBranch, nextCommit.Successor);
            }
        }
    }

    private IEnumerable<Commit> FastForwardBranch(Commit commit)
    {
        var branch = commit.Branch;

        if (branch == null) yield break;

        Commit nextCommit;
        while ((nextCommit = _branchHeads[branch]).Index <= commit.Index)
        {
            // may need to recursively fast forward to handle stacked branches
            if (nextCommit.MergeFrom != null)
            {
                foreach (var branchCommit in FastForwardBranch(nextCommit.MergeFrom))
                    yield return branchCommit;
            }

            yield return nextCommit;
            UpdateHead(branch, nextCommit.Successor);
        }
    }

    private Commit? GetNextCommit()
    {
        Commit? earliest = null;

        foreach (var c in _branchHeads.Values.Where(c => c != EndMarker))
        {
            if (earliest == null || c.Time < earliest.Time)
                earliest = c;
        }

        return earliest;
    }

    private void UpdateHead(string branch, Commit? commit)
    {
        _branchHeads[branch] = commit ?? EndMarker;
    }

    private int CountCommits(Commit? root)
    {
        int count = 0;
        for (Commit? c = root; c != null; c = c.Successor)
            count++;
        return count;
    }
}