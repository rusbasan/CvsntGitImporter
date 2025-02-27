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
/// Unit tests for the MergeResolver class.
/// </summary>
[TestClass]
public class MergeResolverTest
{
    private Mock<ILogger> _logger;
    private FileInfo _file;

    [TestInitialize]
    public void Setup()
    {
        _logger = new Mock<ILogger>();
        _file = new FileInfo("file0");
    }

    [TestMethod]
    public void SingleMerge_NoReordering()
    {
        var streams = CreateSingleMerge();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("initial", "merge"));
        Assert.IsTrue(streams["branch"].ToList().Select(c => c.CommitId).SequenceEqual("branch"));
    }

    [TestMethod]
    public void SingleMerge_MergesFilledIn()
    {
        var streams = CreateSingleMerge();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].Successor.CommitId == "merge" &&
                      streams["MAIN"].Successor.MergeFrom.CommitId == "branch");
        Assert.IsTrue(streams["MAIN"].ToList().Where(c => c.CommitId != "merge").All(c => c.MergeFrom == null));
        Assert.IsTrue(streams["branch"].ToList().All(c => c.MergeFrom == null));
    }

    [TestMethod]
    public void MultipleMerges_NoReordering()
    {
        var streams = CreateMultipleMerges();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("initial", "merge1", "merge2"));
        Assert.IsTrue(streams["branch"].ToList().Select(c => c.CommitId).SequenceEqual("branch1", "branch2"));
    }

    [TestMethod]
    public void MultipleMerges_MergesFilledIn()
    {
        var streams = CreateMultipleMerges();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        var main0 = streams["MAIN"];
        var main1 = main0.Successor;
        var main2 = main1.Successor;

        Assert.IsTrue(main0.CommitId == "initial" && main0.MergeFrom == null);
        Assert.IsTrue(main1.CommitId == "merge1" && main1.MergeFrom.CommitId == "branch1");
        Assert.IsTrue(main2.CommitId == "merge2" && main2.MergeFrom.CommitId == "branch2");
        Assert.IsTrue(streams["branch"].ToList().All(c => c.MergeFrom == null));
    }

    [TestMethod]
    public void CrossedMerge_Reordered()
    {
        var streams = CreateCrossedMerges();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("initial", "merge1", "merge2"));
        Assert.IsTrue(streams["branch"].ToList().Select(c => c.CommitId).SequenceEqual("branch2", "branch1"));
    }

    [TestMethod]
    public void CrossedMerge_MergesFilledIn()
    {
        var streams = CreateCrossedMerges();
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        var main0 = streams["MAIN"];
        var main1 = main0.Successor;
        var main2 = main1.Successor;

        Assert.IsTrue(main0.CommitId == "initial" && main0.MergeFrom == null);
        Assert.IsTrue(main1.CommitId == "merge1" && main1.MergeFrom.CommitId == "branch2");
        Assert.IsTrue(main2.CommitId == "merge2" && main2.MergeFrom.CommitId == "branch1");
        Assert.IsTrue(streams["branch"].ToList().All(c => c.MergeFrom == null));
    }

    [TestMethod]
    public void CrossedMerge_LongerHistoryOnMergeDestination()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial1").WithRevision(_file, "1.1"),
            new Commit("initial2").WithRevision(_file, "1.2"),
            new Commit("initial3").WithRevision(_file, "1.3"),
            new Commit("branch1").WithRevision(_file, "1.3.2.1"),
            new Commit("branch2").WithRevision(_file, "1.3.2.2"),
            new Commit("merge1").WithRevision(_file, "1.4", mergepoint: "1.3.2.2"),
            new Commit("merge2").WithRevision(_file, "1.5", mergepoint: "1.3.2.1"),
        };
        _file.WithBranch("branch", "1.3.0.2");

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        var streams = new BranchStreamCollection(commits, branchpoints);
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId)
            .SequenceEqual("initial1", "initial2", "initial3", "merge1", "merge2"));
        Assert.IsTrue(streams["branch"].ToList().Select(c => c.CommitId).SequenceEqual("branch2", "branch1"));
    }

    [TestMethod]
    public void SingleMergeOnExcludedBranch_NoMergeFilledIn()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial").WithRevision(_file, "1.1"),
            new Commit("merge").WithRevision(_file, "1.2", mergepoint: "1.1.2.1"),
        };

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        var streams = new BranchStreamCollection(commits, branchpoints);
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().All(c => c.MergeFrom == null));
    }

    [TestMethod]
    public void MergeFromParentBranch_Ignore()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial").WithRevision(_file, "1.1"),
            new Commit("branch1").WithRevision(_file, "1.1.2.1"),
            new Commit("main1").WithRevision(_file, "1.2"),
            new Commit("branch2").WithRevision(_file, "1.1.2.2", mergepoint: "1.2"),
        };
        _file.WithBranch("branch", "1.1.0.2");

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        var streams = new BranchStreamCollection(commits, branchpoints);
        var resolver = new MergeResolver(_logger.Object, streams);
        resolver.Resolve();

        Assert.IsTrue(streams["MAIN"].ToList().All(c => c.MergeFrom == null));
        Assert.IsTrue(streams["branch"].ToList().All(c => c.MergeFrom == null));
    }


    private BranchStreamCollection CreateSingleMerge()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial").WithRevision(_file, "1.1"),
            new Commit("branch").WithRevision(_file, "1.1.2.1"),
            new Commit("merge").WithRevision(_file, "1.2", mergepoint: "1.1.2.1"),
        };
        _file.WithBranch("branch", "1.1.0.2");

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        return new BranchStreamCollection(commits, branchpoints);
    }

    private BranchStreamCollection CreateMultipleMerges()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial").WithRevision(_file, "1.1"),
            new Commit("branch1").WithRevision(_file, "1.1.2.1"),
            new Commit("branch2").WithRevision(_file, "1.1.2.2"),
            new Commit("merge1").WithRevision(_file, "1.2", mergepoint: "1.1.2.1"),
            new Commit("merge2").WithRevision(_file, "1.3", mergepoint: "1.1.2.2"),
        };
        _file.WithBranch("branch", "1.1.0.2");

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        return new BranchStreamCollection(commits, branchpoints);
    }

    private BranchStreamCollection CreateCrossedMerges()
    {
        var commits = new List<Commit>()
        {
            new Commit("initial").WithRevision(_file, "1.1"),
            new Commit("branch1").WithRevision(_file, "1.1.2.1"),
            new Commit("branch2").WithRevision(_file, "1.1.2.2"),
            new Commit("merge1").WithRevision(_file, "1.2", mergepoint: "1.1.2.2"),
            new Commit("merge2").WithRevision(_file, "1.3", mergepoint: "1.1.2.1"),
        };
        _file.WithBranch("branch", "1.1.0.2");

        var branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", commits[0] }
        };

        return new BranchStreamCollection(commits, branchpoints);
    }
}