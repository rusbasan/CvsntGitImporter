/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

class ManualBranchResolver : ITagResolver
{
    private readonly ILogger _log;
    private readonly ITagResolver _fallback;
    private readonly ITagResolver _tagResolver;
    private readonly RenameRule _branchpointRule;
    private Dictionary<string, Commit>? _resolvedCommits;
    private IList<Commit>? _commits;

    public ManualBranchResolver(ILogger log, ITagResolver fallbackResolver, ITagResolver tagResolver,
        RenameRule branchpointRule)
    {
        _log = log;
        _fallback = fallbackResolver;
        _tagResolver = tagResolver;
        _branchpointRule = branchpointRule;
    }

    public IDictionary<string, Commit> ResolvedTags
    {
        get { return _resolvedCommits ?? []; }
    }

    public IEnumerable<string> UnresolvedTags
    {
        get { return _fallback.UnresolvedTags; }
    }

    public IEnumerable<Commit> Commits
    {
        get { return _commits ?? []; }
    }

    public bool Resolve(IEnumerable<string> branches, IEnumerable<Commit> commits)
    {
        var rule = _branchpointRule;
        _resolvedCommits = new Dictionary<string, Commit>();
        _commits = commits.ToListIfNeeded();

        _log.DoubleRuleOff();
        _log.WriteLine("Matching branches to branchpoints");
        using (_log.Indent())
        {
            foreach (var branch in branches.Where(b => rule.IsMatch(b)))
            {
                var tag = rule.Apply(branch);

                var commit = ResolveBranchpoint(branch, tag);
                if (commit != null)
                {
                    _resolvedCommits[branch] = commit;
                    _log.WriteLine("Branch {0} -> Tag {1}", branch, tag);
                }
                else
                {
                    _log.WriteLine("Branch {0}: tag {1} is unresolved", branch, tag);
                }
            }
        }

        var otherBranches = branches.Except(_resolvedCommits.Keys).ToList();
        if (otherBranches.Any())
        {
            var result = _fallback.Resolve(otherBranches, _commits);

            foreach (var kvp in _fallback.ResolvedTags)
                _resolvedCommits[kvp.Key] = kvp.Value;

            _commits = _fallback.Commits.ToListIfNeeded();
            return result;
        }
        else
        {
            return true;
        }
    }

    private Commit? ResolveBranchpoint(string branch, string tag)
    {
        if (!_tagResolver.ResolvedTags.TryGetValue(tag, out var branchCommit))
            return null;

        // check for commits to the branch that occur before the tag
        CommitMoveRecord? moveRecord = null;
        foreach (var c in _commits ?? [])
        {
            if (c == branchCommit)
                break;

            if (c.Branch == branch)
            {
                if (moveRecord == null)
                    moveRecord = new CommitMoveRecord(branch, _log) { FinalCommit = branchCommit };
                moveRecord.AddCommit(c, c.Select(r => r.File));
            }
        }

        if (moveRecord != null)
        {
            _log.WriteLine("Some commits on {0} need moving after branchpoint {1}", branch, tag);
            using (_log.Indent())
            {
                moveRecord.Apply(_commits ?? []);
            }
        }

        return branchCommit;
    }
}