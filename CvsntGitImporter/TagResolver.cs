/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Resolves tags to specific commits.
/// </summary>
class TagResolver : AutoTagResolverBase
{
    private readonly ILogger _log;

    public TagResolver(ILogger log, FileCollection allFiles, bool continueOnError, bool noCommitReordering) :
        base(log: log, allFiles: allFiles, false, continueOnError, noCommitReordering: noCommitReordering)
    {
        _log = log;
    }

    public override bool Resolve(IEnumerable<string> tags, IEnumerable<Commit> commits)
    {
        _log.DoubleRuleOff();
        _log.WriteLine("Resolving tags");

        using (_log.Indent())
        {
            return base.Resolve(tags, commits);
        }
    }

    protected override IEnumerable<string> GetTagsForFileRevision(FileInfo file, Revision revision)
    {
        return file.GetTagsForRevision(revision);
    }

    protected override Revision GetRevisionForTag(FileInfo file, string tag)
    {
        return file.GetRevisionForTag(tag);
    }
}