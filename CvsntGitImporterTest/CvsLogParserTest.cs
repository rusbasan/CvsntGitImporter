/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.IO;
using System.Linq;
using System.Text;
using CTC.CvsntGitImporter.TestCode.Properties;
using CTC.CvsntGitImporter.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CTC.CvsntGitImporter.TestCode;

/// <summary>
/// Unit tests for the CvsLogParser class.
/// </summary>
[TestClass]
public class CvsLogParserTest
{
    private TempDir _temp;
    private string _sandbox;
    private InclusionMatcher _branchMatcher;

    [TestInitialize]
    public void Setup()
    {
        _temp = new TempDir();
        Directory.CreateDirectory(_temp.GetPath("CVS"));
        File.WriteAllText(_temp.GetPath(@"CVS\Repository"), "module");
        _sandbox = _temp.Path;
        _branchMatcher = new InclusionMatcher();
    }

    [TestCleanup]
    public void Clearup()
    {
        _temp.Dispose();
    }

    [TestMethod]
    public void StandardFormat()
    {
        var parser = CreateParser(CvsLogParserResources.StandardFormat);
        var revisions = parser.Parse().ToList();

        Assert.AreEqual(revisions.Count(), 2);

        var r = revisions.First();
        Assert.AreEqual(r.Author, "johnb");
        Assert.AreEqual(r.CommitId, "c0449ae6c023bd1");
        Assert.AreEqual(r.File.Name, ".cvsignore");
        Assert.AreEqual(r.IsDead, false);
        Assert.AreEqual(r.Mergepoint, Revision.Empty);
        Assert.AreEqual(r.Revision, Revision.Create("1.2"));
        Assert.AreEqual(r.Time, new DateTime(2009, 3, 4, 11, 54, 43));

        Assert.IsFalse(parser.ExcludedTags.Any(), "No tags excluded");
        Assert.IsFalse(parser.ExcludedBranches.Any(), "No branches excluded");
    }

    [TestMethod]
    public void Mergepoint()
    {
        var parser = CreateParser(CvsLogParserResources.Mergepoint);
        var revisions = parser.Parse().ToList();

        var rev = revisions.First(r => r.Revision == Revision.Create("1.2"));
        Assert.AreEqual(rev.Mergepoint, Revision.Create("1.1.2.1"));
    }

    [TestMethod]
    public void StateDead()
    {
        var parser = CreateParser(CvsLogParserResources.StateDead);
        var revisions = parser.Parse().ToList();

        var r = revisions.First();
        Assert.IsTrue(r.IsDead);
    }

    [TestMethod]
    public void FileAddedOnBranch()
    {
        var parser = CreateParser(CvsLogParserResources.FileAddedOnBranch);
        var revisions = parser.Parse().ToList();

        Assert.AreEqual(revisions[1].Revision.ToString(), "1.1");
        Assert.IsTrue(revisions[1].IsDead);
    }

    [TestMethod]
    public void NoCommitId()
    {
        var parser = CreateParser(CvsLogParserResources.MissingCommitId);
        var revisions = parser.Parse().ToList();

        Assert.AreEqual(revisions.Count, 2);
        Assert.IsTrue(revisions.All(r => r.CommitId == ""));
    }

    [TestMethod]
    public void ExcludeBranches()
    {
        _branchMatcher.AddExcludeRule(@"^branch2");
        _branchMatcher.AddIncludeRule(@"^branch1");

        var parser = CreateParser(CvsLogParserResources.Branches);
        parser.Parse().ToList();
        var file = parser.Files.Single();

        Assert.AreEqual(file.GetBranchpointForBranch("branch1"), Revision.Create("1.1"));
        Assert.AreEqual(file.GetBranchpointForBranch("branch2"), Revision.Empty);
        Assert.AreEqual(parser.ExcludedBranches.Single(), "branch2");
    }

    [TestMethod]
    public void NonAsciiFile()
    {
        using (var temp = new TempDir())
        {
            // write the log file in the default encoding, which is what the CVS log will typically be in
            var cvsLog = temp.GetPath("cvs.log");
            File.WriteAllText(cvsLog, CvsLogParserResources.NonAscii, Encoding.Default);

            var parser = new CvsLogParser(_sandbox, cvsLog, _branchMatcher, _ => false);
            parser.Parse().ToList();
            var file = parser.Files.Single();

            Assert.AreEqual(file.Name, "demo©.xje");
        }
    }


    private CvsLogParser CreateParser(string log)
    {
        return new CvsLogParser(_sandbox, new StringReader(log), _branchMatcher, _ => false);
    }
}