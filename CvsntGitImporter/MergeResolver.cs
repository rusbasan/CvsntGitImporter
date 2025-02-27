/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Resolve merges back to individual commits on other branches.
/// </summary>
class MergeResolver
{
    private readonly ILogger _log;
    private readonly BranchStreamCollection _streams;

    public MergeResolver(ILogger log, BranchStreamCollection streams)
    {
        _log = log;
        _streams = streams;
    }

    public void Resolve()
    {
        _log.DoubleRuleOff();
        _log.WriteLine("Resolving merges...");

        using (_log.Indent())
        {
            ResolveMerges();
        }
    }

    private void ResolveMerges()
    {
        int failures = 0;
        foreach (var branch in _streams.Branches)
        {
            _log.WriteLine("{0}", branch);
            using (_log.Indent())
            {
                var branchRoot = _streams[branch];
                failures += ProcessBranch(branchRoot);
            }
        }

        if (failures > 0)
            throw new ImportFailedException("Failed to resolve all merges");
    }

    /// <summary>
    /// Process merges to a single branch.
    /// </summary>
    /// <returns>Number of failures</returns>
    private int ProcessBranch(Commit? branchDestRoot)
    {
        int failures = 0;
        var lastMerges = new Dictionary<string, Commit>();
        Func<string, Commit?> getLastMerge = branchFrom =>
        {
            return lastMerges.TryGetValue(branchFrom, out var result) ? result : null;
        };

        for (Commit? commitDest = branchDestRoot; commitDest != null; commitDest = commitDest.Successor)
        {
            if (!commitDest.MergedFiles.Any())
                continue;

            // get the last commit on the source branch for all the merged files
            var commitSource = commitDest.MergedFiles
                .Select(f => f.File.GetCommit(f.Mergepoint))
                .Where(c => c != null)
                .OrderByDescending(c => c?.Index)
                .FirstOrDefault();

            // ignore excluded branches
            if (commitSource == null)
                continue;

            var commitBranchRoot = commitSource.Branch != null ? _streams[commitSource.Branch] : null ;
            if (commitBranchRoot?.Predecessor == null || commitBranchRoot.Predecessor.Branch != commitDest.Branch)
            {
                _log.WriteLine(
                    "Warning: ignoring merge to commit {0} - merged commit {1} is on {2} which is not branched off from {3}",
                    commitDest.CommitId, commitSource.CommitId, commitSource.Branch ?? String.Empty, commitDest.Branch ?? String.Empty);
                continue;
            }

            var lastMergeSource = commitSource.Branch != null ? getLastMerge(commitSource.Branch) : null;
            if (lastMergeSource != null && commitSource.Index < lastMergeSource.Index)
            {
                _log.WriteLine("Merges from {0} to {1} are crossed ({2}->{3})",
                    commitSource.Branch ?? String.Empty, commitDest.Branch ?? String.Empty, commitSource.CommitId, commitDest.CommitId);

                if (commitSource.Branches.Any())
                {
                    _log.WriteLine("Warning: not moving {0} as it is a branchpoint for {1}", commitSource.CommitId,
                        String.Join(", ", commitSource.Branches.Select(c => c.Branch)));
                    continue;
                }
                else if (lastMergeSource.Branches.Any())
                {
                    _log.WriteLine("Warning: not moving {0} as it is a branchpoint for {1}", lastMergeSource.CommitId,
                        String.Join(", ", lastMergeSource.Branches.Select(c => c.Branch)));
                    continue;
                }
                else
                {
                    using (_log.Indent())
                    {
                        _streams.MoveCommit(commitSource, lastMergeSource);

                        // don't update last merge as it has not changed
                    }
                }
            }
            else
            {
                if (commitSource.Branch != null)
                    lastMerges[commitSource.Branch] = commitSource;
            }

            // fill in the resolved merge
            commitDest.MergeFrom = commitSource;
        }

        return failures;
    }
}