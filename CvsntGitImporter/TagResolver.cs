/*
 * John Hall <john.hall@camtechconsultants.com>
 * � 2013-2022 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter
{
	/// <summary>
	/// Resolves tags to specific commits.
	/// </summary>
	class TagResolver : AutoTagResolverBase
	{
		private readonly ILogger m_log;

		public TagResolver(ILogger log, FileCollection allFiles, bool continueOnError, bool noCommitReordering) :
				base(log: log, allFiles: allFiles, false, continueOnError, noCommitReordering: noCommitReordering)
		{
			m_log = log;
		}

		public override bool Resolve(IEnumerable<string> tags, IEnumerable<Commit> commits)
		{
			m_log.DoubleRuleOff();
			m_log.WriteLine("Resolving tags");

			using (m_log.Indent())
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
}