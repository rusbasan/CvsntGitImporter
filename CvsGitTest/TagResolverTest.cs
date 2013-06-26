﻿/*
 * John Hall <john.hall@camtechconsultants.com>
 * Copyright (c) Cambridge Technology Consultants Ltd. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using CTC.CvsntGitImporter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhino.Mocks;

namespace CTC.CvsntGitImporter.TestCode
{
	/// <summary>
	/// Unit tests for the TagResolver class.
	/// </summary>
	[TestClass]
	public class TagResolverTest
	{
		private ILogger m_logger;

		[TestInitialize]
		public void Setup()
		{
			m_logger = MockRepository.GenerateStub<ILogger>();
		}


		[TestMethod]
		public void Resolve_ReorderCommits()
		{
			var commits = CreateCommitThatNeedsReordering();
			var orderBefore = commits.ToList();
			var allFiles = commits.CreateAllFiles();

			var resolver = new TagResolver(m_logger, commits, allFiles, new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Succeeded");
			Assert.IsTrue(resolver.Commits.SequenceEqual(orderBefore[0], orderBefore[2], orderBefore[1]), "Commits reordered");
		}

		[TestMethod]
		public void Resolve_SplitCommit()
		{
			var commits = CreateCommitThatNeedsSplitting().ToList();
			var orderBefore = commits.ToList();
			var allFiles = commits.CreateAllFiles();

			var resolver = new TagResolver(m_logger, commits, allFiles, new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Succeeded");
			var newCommits = resolver.Commits.ToList();
			Assert.AreEqual(newCommits.Count, 4);
			Assert.AreEqual(newCommits[0], orderBefore[0]);
			Assert.AreEqual(newCommits[1], orderBefore[1]);
			Assert.IsTrue(newCommits[2].Single().File.Name == "file2" && newCommits[2].Single().Revision.Equals(Revision.Create("1.2")));
			Assert.IsTrue(newCommits[3].Single().File.Name == "file1" && newCommits[3].Single().Revision.Equals(Revision.Create("1.3")));
		}

		[TestMethod]
		public void Resolve_ReorderWithCreatedFileInTheMiddle()
		{
			var commits = CreateCommitThatNeedsReordering(addFileInMiddle: true).ToList();
			var orderBefore = commits.ToList();
			var allFiles = commits.CreateAllFiles();

			var resolver = new TagResolver(m_logger, commits, allFiles, new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Succeeded");
			Assert.AreEqual(resolver.Commits.Count(), 3, "No split");
			Assert.IsTrue(resolver.Commits.SequenceEqual(orderBefore[0], orderBefore[2], orderBefore[1]), "Commits reordered");
			Assert.AreSame(resolver.ResolvedCommits["tag"], orderBefore[2]);
		}

		[TestMethod]
		public void Resolve_ReorderWithCreatedAndModifiedFileInTheMiddle()
		{
			var commits = CreateCommitWithAddedAndModifiedFileInTheMiddle().ToList();
			var orderBefore = commits.ToList();
			var allFiles = commits.CreateAllFiles();

			var resolver = new TagResolver(m_logger, commits, allFiles, new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Succeeded");
			Assert.AreEqual(resolver.Commits.Count(), 4, "No split");
			Assert.IsTrue(resolver.Commits.SequenceEqual(orderBefore[0], orderBefore[3], orderBefore[1], orderBefore[2]), "Commits reordered");
			Assert.AreSame(resolver.ResolvedCommits["tag"], orderBefore[3]);
		}

		[TestMethod]
		public void Resolve_FileDeletedBeforeTag()
		{
			var file1 = new FileInfo("file1").WithTag("tag", "1.2");
			var file2 = new FileInfo("file2");

			var commits = new List<Commit>()
			{
				new Commit("c0").WithRevision(file1, "1.1").WithRevision(file2, "1.1"),
				new Commit("c1").WithRevision(file2, "1.2", isDead: true),
				new Commit("c2").WithRevision(file1, "1.2"),
			};

			var resolver = new TagResolver(m_logger, commits, commits.CreateAllFiles(), new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Resolve succeeded");
			Assert.AreSame(resolver.ResolvedCommits["tag"], commits[2]);
			Assert.IsTrue(resolver.Commits.SequenceEqual(commits), "Commits not reordered");
		}

		[TestMethod]
		public void Resolve_FileDeletedBeforeTag_ReorderingRequired()
		{
			var file1 = new FileInfo("file1").WithTag("tag", "1.1");
			var file2 = new FileInfo("file2");
			var file3 = new FileInfo("file3").WithTag("tag", "1.2");

			var commits = new List<Commit>()
			{
				new Commit("c0").WithRevision(file1, "1.1").WithRevision(file2, "1.1").WithRevision(file3, "1.1"),
				new Commit("c1").WithRevision(file2, "1.2", isDead: true),
				new Commit("c2").WithRevision(file1, "1.2"),
				new Commit("c3").WithRevision(file3, "1.2"),
			};
			var orderBefore = new List<Commit>(commits);

			var resolver = new TagResolver(m_logger, commits, commits.CreateAllFiles(), new InclusionMatcher());
			var result = resolver.Resolve(new[] { "tag" });

			Assert.IsTrue(result, "Resolve succeeded");
			Assert.AreSame(resolver.ResolvedCommits["tag"], orderBefore[3]);
			Assert.IsTrue(resolver.Commits.SequenceEqual(orderBefore[0], orderBefore[1], orderBefore[3], orderBefore[2]));
		}


		private static IEnumerable<Commit> CreateCommitThatNeedsReordering(bool addFileInMiddle = false)
		{
			var file1 = new FileInfo("file1").WithTag("tag", "1.1");
			var file2 = new FileInfo("file2").WithTag("tag", "1.2");
			var file3 = new FileInfo("file3");

			var commits = new List<Commit>();

			commits.Add(new Commit("id0")
					.WithRevision(file1, "1.1")
					.WithRevision(file2, "1.1"));

			var commit1 = new Commit("id1")
					.WithRevision(file1, "1.2");
			if (addFileInMiddle)
				commit1.WithRevision(file3, "1.1");
			commits.Add(commit1);

			commits.Add(new Commit("id2")
					.WithRevision(file2, "1.2"));

			return commits;
		}

		private static IEnumerable<Commit> CreateCommitWithAddedAndModifiedFileInTheMiddle()
		{
			var file1 = new FileInfo("file1").WithTag("tag", "1.1");
			var file2 = new FileInfo("file2").WithTag("tag", "1.2");
			var file3 = new FileInfo("file3");

			var commits = new List<Commit>();

			commits.Add(new Commit("id0")
					.WithRevision(file1, "1.1")
					.WithRevision(file2, "1.1"));

			commits.Add(new Commit("id1")
					.WithRevision(file3, "1.1"));  // file3 added

			commits.Add(new Commit("id2")
					.WithRevision(file3, "1.2"));  // file3 modified

			// this is the target commit for "tag"
			commits.Add(new Commit("id3")
					.WithRevision(file2, "1.2"));

			return commits;
		}

		private static IEnumerable<Commit> CreateCommitThatNeedsSplitting()
		{
			var file1 = new FileInfo("file1").WithTag("tag", "1.2");
			var file2 = new FileInfo("file2").WithTag("tag", "1.2");

			var commit0 = new Commit("id0")
					.WithRevision(file1, "1.1")
					.WithRevision(file2, "1.1");

			var commit1 = new Commit("id1")
					.WithRevision(file1, "1.2");

			var commit2 = new Commit("id2")
					.WithRevision(file1, "1.3")
					.WithRevision(file2, "1.2");

			return new[] { commit0, commit1, commit2 };
		}
	}
}