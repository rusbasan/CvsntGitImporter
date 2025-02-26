/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CTC.CvsntGitImporter.TestCode;

/// <summary>
/// Unit tests for the ManualBranchResolver class
/// </summary>
[TestClass]
public class ManualBranchResolverTest
{
    private Mock<ILogger> _log;
    private Mock<ITagResolver> _tagResolver;
    private RenameRule _rule;

    public ManualBranchResolverTest()
    {
        _log = new Mock<ILogger>();
        _tagResolver = new Mock<ITagResolver>();
        _rule = new RenameRule(@"^(.*)", "$1-branchpoint");
    }

    [TestMethod]
    public void Resolve_BranchpointTagExists()
    {
        var fallback = new Mock<ITagResolver>();

        var commit1 = new Commit("c1");
        var resolvedTags = new Dictionary<string, Commit>()
        {
            { "branch-branchpoint", commit1 },
        };
        _tagResolver.Setup(tr => tr.ResolvedTags).Returns(resolvedTags);

        var resolver = new ManualBranchResolver(_log.Object, fallback.Object, _tagResolver.Object, _rule);
        bool result = resolver.Resolve(new[] { "branch" }, new[] { commit1 });

        Assert.IsTrue(result, "Resolved");
        Assert.AreSame(resolver.ResolvedTags["branch"], commit1);
        fallback.Verify(f => f.Resolve(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Commit>>()), Times.Never);
    }

    [TestMethod]
    public void Resolve_BranchpointTagDoesNotExist_FallBackToAuto()
    {
        var commit1 = new Commit("c1");
        var resolvedCommits = new Dictionary<string, Commit>()
        {
            { "branch", commit1 }
        };

        var fallback = new Mock<ITagResolver>();
        fallback.Setup(f => f.Resolve(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Commit>>())).Returns(true);
        fallback.Setup(f => f.ResolvedTags).Returns(resolvedCommits);
        fallback.Setup(f => f.Commits).Returns(new[] { commit1 });

        var resolvedTags = new Dictionary<string, Commit>();
        _tagResolver.Setup(tr => tr.ResolvedTags).Returns(resolvedTags);

        var resolver = new ManualBranchResolver(_log.Object, fallback.Object, _tagResolver.Object, _rule);
        bool result = resolver.Resolve(new[] { "branch" }, new[] { commit1 });

        Assert.IsTrue(result, "Resolved");
        Assert.AreSame(resolver.ResolvedTags["branch"], commit1);
    }

    [TestMethod]
    public void Resolve_ResolveFails()
    {
        var resolvedCommits = new Dictionary<string, Commit>();

        var fallback = new Mock<ITagResolver>();
        fallback.Setup(f => f.Resolve(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Commit>>())).Returns(false);
        fallback.Setup(f => f.ResolvedTags).Returns(resolvedCommits);
        fallback.Setup(f => f.UnresolvedTags).Returns(new[] { "branch" });
        fallback.Setup(f => f.Commits).Returns(Enumerable.Empty<Commit>());

        var resolvedTags = new Dictionary<string, Commit>();
        _tagResolver.Setup(tr => tr.ResolvedTags).Returns(resolvedTags);

        var resolver = new ManualBranchResolver(_log.Object, fallback.Object, _tagResolver.Object, _rule);
        bool result = resolver.Resolve(new[] { "branch" }, Enumerable.Empty<Commit>());

        Assert.IsFalse(result, "Resolved");
        Assert.IsTrue(resolver.UnresolvedTags.SequenceEqual("branch"));
    }

    [TestMethod]
    public void Resolve_CommitOnBranchBeforeBranchpointTag()
    {
        var f1 = new FileInfo("file1").WithBranch("branch", "1.2.0.2").WithTag("branch-branchpoint", "1.2");
        var f2 = new FileInfo("file2").WithBranch("branch", "1.1.0.2").WithTag("branch-branchpoint", "1.1");

        var commits = new List<Commit>()
        {
            new Commit("c0").WithRevision(f1, "1.1").WithRevision(f2, "1.1"),
            new Commit("c1").WithRevision(f2, "1.1.2.1"),
            new Commit("c2").WithRevision(f1, "1.2"),
        };

        var fallback = new Mock<ITagResolver>();

        var resolvedTags = new Dictionary<string, Commit>()
        {
            { "branch-branchpoint", commits[2] },
        };
        _tagResolver.Setup(tr => tr.ResolvedTags).Returns(resolvedTags);

        var resolver = new ManualBranchResolver(_log.Object, fallback.Object, _tagResolver.Object, _rule);
        bool result = resolver.Resolve(new[] { "branch" }, commits);

        Assert.IsTrue(result, "Resolved");
        Assert.AreEqual(resolver.ResolvedTags["branch"].CommitId, "c2");
        Assert.IsTrue(resolver.Commits.Select(c => c.CommitId).SequenceEqual("c0", "c2", "c1"), "Commits reordered");
    }
}