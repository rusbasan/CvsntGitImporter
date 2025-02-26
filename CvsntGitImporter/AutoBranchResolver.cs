/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
*/

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Resolves branches to specific commits.
/// </summary>
class AutoBranchResolver : AutoTagResolverBase
{
    private readonly ILogger _log;

    public AutoBranchResolver(ILogger log, FileCollection allFiles, bool continueOnError, bool noCommitReordering) :
        base(log: log, allFiles: allFiles, branches: true, continueOnError: continueOnError,
            noCommitReordering: noCommitReordering)
    {
        _log = log;
    }

    public override bool Resolve(IEnumerable<string> tags, IEnumerable<Commit> commits)
    {
        _log.DoubleRuleOff();
        _log.WriteLine("Resolving branches");

        using (_log.Indent())
        {
            return base.Resolve(tags, commits);
        }
    }

    protected override IEnumerable<string> GetTagsForFileRevision(FileInfo file, Revision revision)
    {
        return file.GetBranchesAtRevision(revision);
    }

    protected override Revision GetRevisionForTag(FileInfo file, string tag)
    {
        return file.GetBranchpointForBranch(tag);
    }

    protected override void HandleMissingFiles(string tag, List<Commit> commits, IEnumerable<FileInfo> files,
        CommitMoveRecord moveRecord, ref Commit candidate)
    {
        var filteredFiles = files.Where(f => f.BranchAddedOn != tag);
        base.HandleMissingFiles(tag, commits, filteredFiles, moveRecord, ref candidate);
    }
}