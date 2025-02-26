/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Linq;
using CTC.CvsntGitImporter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CTC.CvsntGitImporter.TestCode;

/// <summary>
/// Unit tests for Commit class.
/// </summary>
[TestClass]
public class CommitTest
{
    private FileInfo _f1;
    private FileInfo _f2;
    private FileInfo _f3;

    [TestInitialize]
    public void Setup()
    {
        _f1 = new FileInfo("f1");
        _f2 = new FileInfo("f2");
        _f3 = new FileInfo("f3");
    }


    #region MergedFiles

    [TestMethod]
    public void MergedFiles_None()
    {
        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2");
        var result = commit.MergedFiles;

        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public void MergedFiles_All()
    {
        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
            .WithRevision(_f2, "1.3", mergepoint: "1.1.4.2");
        var result = commit.MergedFiles;

        Assert.AreEqual(result.Count(), 2);
    }

    [TestMethod]
    public void MergedFiles_Mixture()
    {
        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
            .WithRevision(_f2, "1.3");
        var result = commit.MergedFiles;

        Assert.AreEqual(result.Single().File.Name, "f1");
    }

    #endregion MergedFiles


    #region Verify

    [TestMethod]
    public void Verify_MergeFromTwoBranches()
    {
        _f1.WithBranch("branch1", "1.1.0.2");
        _f2.WithBranch("branch2", "1.1.0.2");

        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
            .WithRevision(_f2, "1.2", mergepoint: "1.1.2.1");
        commit.Verify();

        Assert.IsTrue(commit.Errors.Single().Contains("Multiple branches merged from"));
    }

    [TestMethod]
    public void Verify_MergeFromTwoBranches_OneIsExcluded()
    {
        _f1.WithBranch("branch1", "1.1.0.2");

        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
            .WithRevision(_f2, "1.2", mergepoint: "1.1.2.1");
        var result = commit.Verify();

        Assert.IsTrue(result, "Verification succeeded");
    }

    [TestMethod]
    public void Verify_MergeFromTwoBranchesAndNonMerge()
    {
        _f1.WithBranch("branch1", "1.1.0.2");
        _f2.WithBranch("branch2", "1.1.0.2");

        var commit = new Commit("abc")
            .WithRevision(_f3, "1.1")
            .WithRevision(_f1, "1.2", mergepoint: "1.1.2.1")
            .WithRevision(_f2, "1.2", mergepoint: "1.1.2.1");
        commit.Verify();

        Assert.IsTrue(commit.Errors.Single().Contains("Multiple branches merged from"));
    }

    [TestMethod]
    public void Verify_MergeFromParallelBranch_WithUnmodifiedFileOnSourceBranch()
    {
        _f1.WithBranch("branch1", "1.1.0.2").WithBranch("branch2", "1.2.0.2");
        _f2.WithBranch("branch1", "1.1.0.2").WithBranch("branch2", "1.2.0.2");

        var commit = new Commit("abc")
            .WithRevision(_f1, "1.2.2.1", mergepoint: "1.1.2.1") // file was modified on branch1
            .WithRevision(_f2, "1.2.2.1", mergepoint: "1.1"); // file was not modified on branch1
        bool result = commit.Verify();

        Assert.IsTrue(result, "Verification succeeded");
    }

    #endregion Verify


    #region IsBranchpoint

    [TestMethod]
    public void IsBranchpoint_NoBranches()
    {
        var commit = new Commit("abc").WithRevision(_f1, "1.1");

        Assert.IsFalse(commit.IsBranchpoint);
    }

    [TestMethod]
    public void IsBranchpoint_WithBranches()
    {
        _f1.WithBranch("branch", "1.1.0.2");
        var commit = new Commit("main1").WithRevision(_f1, "1.1");
        var branchCommit = new Commit("branch1").WithRevision(_f1, "1.1.2.1");
        commit.AddBranch(branchCommit);

        Assert.IsTrue(commit.IsBranchpoint);
    }

    #endregion IsBranchpoint


    #region ReplaceBranch

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReplaceBranch_NonExistent()
    {
        var commit = new Commit("main1").WithRevision(_f1, "1.1");
        var branchCommit1 = new Commit("branch1").WithRevision(_f1, "1.1.2.1");
        var branchCommit2 = new Commit("branch2").WithRevision(_f1, "1.1.2.2");

        commit.ReplaceBranch(branchCommit1, branchCommit2);
    }

    [TestMethod]
    public void ReplaceBranch()
    {
        _f1.WithBranch("branch", "1.1.0.2");
        var commit = new Commit("main1").WithRevision(_f1, "1.1");
        var branchCommit1 = new Commit("branch1").WithRevision(_f1, "1.1.2.1");
        var branchCommit2 = new Commit("branch2").WithRevision(_f1, "1.1.2.2");
        commit.AddBranch(branchCommit1);

        commit.ReplaceBranch(branchCommit1, branchCommit2);

        Assert.IsTrue(commit.IsBranchpoint);
        Assert.IsTrue(commit.Branches.Single() == branchCommit2);
    }

    #endregion ReplaceBranch
}