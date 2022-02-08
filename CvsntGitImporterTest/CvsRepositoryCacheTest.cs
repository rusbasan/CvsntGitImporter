/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2022 Cambridge Technology Consultants Ltd.
 */

using System;
using System.IO;
using CTC.CvsntGitImporter.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CTC.CvsntGitImporter.TestCode
{
	/// <summary>
	/// Unit tests for the CvsRepositoryCache class.
	/// </summary>
	[TestClass]
	public class CvsRepositoryCacheTest
	{
		private TempDir m_temp;

		[TestInitialize]
		public void Setup()
		{
			m_temp = new TempDir();
		}

		[TestCleanup]
		public void Clearup()
		{
			m_temp.Dispose();
		}

		[TestMethod]
		public void Construct_CreatesDirectoryIfMissing()
		{
			var cacheDir = m_temp.GetPath("dir");
			var cache = new CvsRepositoryCache(cacheDir, new Mock<ICvsRepository>().Object);

			Assert.IsTrue(Directory.Exists(cacheDir));
		}

		[TestMethod]
		public void GetCvsRevision_CallsUnderlyingIfFileMissing()
		{
			var f = new FileRevision(new FileInfo("file.txt"), Revision.Create("1.1"),
					mergepoint: Revision.Empty,
					time: DateTime.Now,
					author: "fred",
					commitId: "c1");

			var repo = new Mock<ICvsRepository>();
			repo.Setup(r => r.GetCvsRevision(f)).Returns(new FileContent("file.txt", FileContentData.Empty)).Verifiable();
			var cache = new CvsRepositoryCache(m_temp.Path, repo.Object);
			cache.GetCvsRevision(f);

			repo.VerifyAll();
		}

		[TestMethod]
		public void GetCvsRevision_ReturnsExistingFileIfPresent()
		{
			var f = new FileRevision(new FileInfo("file.txt"), Revision.Create("1.1"),
					mergepoint: Revision.Empty,
					time: DateTime.Now,
					author: "fred",
					commitId: "c1");

			var contents = new FileContentData(new byte[] { 1, 2, 3, 4 }, 4);
			var repo1 = new Mock<ICvsRepository>();
			repo1.Setup(r => r.GetCvsRevision(f)).Returns(new FileContent("file.txt", contents));
			var cache1 = new CvsRepositoryCache(m_temp.Path, repo1.Object);
			cache1.GetCvsRevision(f);

			// create a second cache
			var repo2 = new Mock<ICvsRepository>();
			var cache2 = new CvsRepositoryCache(m_temp.Path, repo1.Object);
			var data = cache2.GetCvsRevision(f);

			repo2.Verify(r => r.GetCvsRevision(f), Times.Never);
			Assert.AreNotSame(data.Data, contents);
			Assert.IsTrue(data.Data.Equals(contents));
		}
	}
}