/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Linq;
using System.Collections.Generic;
using CTC.CvsntGitImporter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CTC.CvsntGitImporter.TestCode;

[TestClass]
public class BranchStreamCollectionTest
{
    private FileInfo _f1;
    private List<Commit> _commits;
    private Dictionary<string, Commit> _branchpoints;

    [TestInitialize]
    public void TestSetup()
    {
        _f1 = new FileInfo("f1").WithBranch("branch", "1.1.0.2");

        _commits = new List<Commit>()
        {
            new Commit("1").WithRevision(_f1, "1.1"),
            new Commit("2").WithRevision(_f1, "1.1.2.1"),
            new Commit("3").WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
        };

        _branchpoints = new Dictionary<string, Commit>()
        {
            { "branch", _commits[0] },
        };
    }

    #region Construct

    [TestMethod]
    public void Construct()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("1", "3"));
        Assert.IsTrue(streams["branch"].ToList().Single().CommitId == "2");
        Assert.IsTrue(streams.Verify());
    }

    [TestMethod]
    public void Construct_IgnoredBranch()
    {
        // remove 'branch' from the list of branchpoints, simulating an ignored branch
        _branchpoints.Remove("branch");
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsFalse(streams["branch"].ToList().Any());
    }

    [TestMethod]
    public void Construct_BranchPredecessorSet()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsTrue(streams["branch"].ToList().Single().Predecessor == _commits[0]);
    }

    #endregion Construct


    #region Properties

    [TestMethod]
    public void Roots()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsTrue(streams["MAIN"] == _commits[0]);
        Assert.IsTrue(streams["branch"] == _commits[1]);
    }

    [TestMethod]
    public void Heads()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsTrue(streams.Head("MAIN") == _commits[2]);
        Assert.IsTrue(streams.Head("branch") == _commits[1]);
    }

    [TestMethod]
    public void Branches()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        Assert.IsTrue(
            streams.Branches.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).SequenceEqual("branch", "MAIN"));
    }

    #endregion Properties


    #region AppendCommit

    [TestMethod]
    public void AppendCommit_ToMain()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        var commit = new Commit("new").WithRevision(_f1, "1.3");
        streams.AppendCommit(commit);

        Assert.AreSame(streams.Head("MAIN"), commit);
        Assert.AreSame(streams["MAIN"].Successor.Successor, commit);
        Assert.IsTrue(commit.Index > streams["MAIN"].Successor.Index, "Index set");
    }

    [TestMethod]
    public void AppendCommit_ToBranch()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        var commit = new Commit("new").WithRevision(_f1, "1.1.2.2");
        streams.AppendCommit(commit);

        Assert.AreSame(streams.Head("branch"), commit);
        Assert.AreSame(streams["branch"].Successor, commit);
        Assert.IsTrue(commit.Index > streams["MAIN"].Index, "Index set");
    }

    #endregion AppendCommit


    #region MoveCommit

    [TestMethod]
    public void MoveCommit_ToItself()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        streams.MoveCommit(_commits[2], _commits[2]);

        Assert.IsTrue(streams["MAIN"].ToList().SequenceEqual(_commits[0], _commits[2]));
        Assert.IsTrue(streams.Verify());
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void MoveCommit_Backwards()
    {
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        streams.MoveCommit(_commits[2], _commits[1]);
    }

    [TestMethod]
    public void MoveCommit_Forwards()
    {
        _commits.Add(new Commit("4").WithRevision(_f1, "1.3"));
        _commits.Add(new Commit("5").WithRevision(_f1, "1.4"));
        _commits.Add(new Commit("6").WithRevision(_f1, "1.5"));
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        streams.MoveCommit(_commits[2], _commits[4]);

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("1", "4", "5", "3", "6"));
        Assert.IsTrue(streams.Verify());
    }

    [TestMethod]
    public void MoveCommit_ToEnd()
    {
        _commits.Add(new Commit("4").WithRevision(_f1, "1.3"));
        _commits.Add(new Commit("5").WithRevision(_f1, "1.4"));
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        streams.MoveCommit(_commits[3], _commits[4]);

        Assert.IsTrue(streams["MAIN"].ToList().Select(c => c.CommitId).SequenceEqual("1", "3", "5", "4"));
        Assert.IsTrue(streams.Head("MAIN").CommitId == "4");
        Assert.IsTrue(streams.Verify());
    }

    [TestMethod]
    public void MoveCommit_FromStart()
    {
        _commits.Add(new Commit("4").WithRevision(_f1, "1.1.2.2"));
        _commits.Add(new Commit("5").WithRevision(_f1, "1.1.2.3"));
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        streams.MoveCommit(_commits[1], _commits[4]);

        Assert.IsTrue(streams["branch"].CommitId == "4");
        Assert.IsTrue(streams["branch"].ToList().Select(c => c.CommitId).SequenceEqual("4", "5", "2"));
        Assert.IsTrue(streams.Verify());
    }

    #endregion MoveCommit


    #region OrderedBranches

    [TestMethod]
    public void OrderedBranches()
    {
        Commit subBranchPoint;
        _commits.AddRange(new[]
        {
            new Commit("4").WithRevision(_f1, "1.3"),
            new Commit("5").WithRevision(_f1, "1.1.2.2"),
            subBranchPoint = new Commit("6").WithRevision(_f1, "1.1.2.3"),
            new Commit("7").WithRevision(_f1, "1.1.2.3.2.1"),
        });

        _f1.WithBranch("subbranch", "1.1.2.3.0.2");
        _branchpoints["subbranch"] = subBranchPoint;
        var streams = new BranchStreamCollection(_commits, _branchpoints);

        var orderedBranches = streams.OrderedBranches.ToList();
        Assert.IsTrue(orderedBranches.SequenceEqual("MAIN", "branch", "subbranch"));
    }

    #endregion OrderedBranches
}