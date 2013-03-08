/*
 * John Hall <john.hall@xjtag.com>
 * Copyright (c) Midas Yellow Ltd. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace CvsGitConverter
{
	/// <summary>
	/// Resolves tags to specific commits.
	/// </summary>
	class TagResolver
	{
		private readonly IEnumerable<Commit> m_commits;
		private readonly Dictionary<string, FileInfo> m_allFiles;
		private List<string> m_errors;

		public TagResolver(IEnumerable<Commit> commits, Dictionary<string, FileInfo> allFiles)
		{
			m_commits = commits;
			m_allFiles = allFiles;
		}

		/// <summary>
		/// Gets any errors encountered resolving tags.
		/// </summary>
		public IEnumerable<string> Errors
		{
			get { return (m_errors == null) ? Enumerable.Empty<string>() : m_errors; }
		}

		/// <summary>
		/// Resolve tags. Find what tags each commit contributes to and build a stack for each tag.
		/// The last commit that contributes to a tag should be the one that we tag.
		/// </summary>
		public void Resolve()
		{
			m_errors = null;

			var tags = FindCommitsPerTag();
			var candidateCommits = FindCandidateCommits(tags);

			// now replay commits and check that all files are in the correct state for each tag
			var state = new RepositoryState();
			foreach (var commit in m_commits)
			{
				state.Apply(commit);

				// if the commit is a candidate for being tagged, then check that all files are at the correct version
				if (candidateCommits.ContainsKey(commit))
				{
					foreach (var tag in candidateCommits[commit])
					{
						var branchState = state[commit.Branch];
						foreach (var filename in branchState.LiveFiles)
						{
							var file = m_allFiles[filename];
							if (!file.GetTags(branchState[filename]).Contains(tag))
							{
								AddError("No commit found for tag. Tag: {0}  Commit: {1}  File: {2},r{3}",
										tag, commit.CommitId, filename, branchState[filename]);
							}
						}
					}
				}
			}
		}

		private Dictionary<string, Stack<Commit>> FindCommitsPerTag()
		{
			var tags = new Dictionary<string, Stack<Commit>>();

			foreach (var commit in m_commits)
			{
				foreach (var file in commit)
				{
					foreach (var tag in file.File.GetTags(file.Revision))
					{
						Stack<Commit> commitsForTag;
						if (tags.TryGetValue(tag, out commitsForTag))
						{
							if (commitsForTag.Peek().CommitId != commit.CommitId)
								commitsForTag.Push(commit);
						}
						else
						{
							commitsForTag = new Stack<Commit>();
							commitsForTag.Push(commit);
							tags[tag] = commitsForTag;
						}
					}
				}
			}

			return tags;
		}

		/// <summary>
		/// Build the inverse of CommitsPerTag - a lookup of commits to tags that it is supposed to be the commit for.
		/// </summary>
		private static Dictionary<Commit, List<string>> FindCandidateCommits(Dictionary<string, Stack<Commit>> tags)
		{
			var candidateCommits = new Dictionary<Commit, List<string>>(CommitComparer.ById);

			foreach (var kvp in tags)
			{
				var commit = kvp.Value.Peek();

				List<string> tagsForCommit;
				if (candidateCommits.TryGetValue(commit, out tagsForCommit))
					tagsForCommit.Add(kvp.Key);
				else
					candidateCommits[commit] = new List<string>(1) { kvp.Key };
			}

			return candidateCommits;
		}

		private void AddError(string format, params object[] args)
		{
			if (m_errors == null)
				m_errors = new List<string>();
			m_errors.Add(String.Format(format, args));
		}
	}
}
