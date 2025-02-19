/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2022 Cambridge Technology Consultants Ltd.
 */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CTC.CvsntGitImporter.TestCode;

/// <summary>
/// Unit tests for the UnixTime class.
/// </summary>
[TestClass]
public class UnixTimeTest
{
	[TestMethod]
	public void FromDateTime_PureDate()
	{
		var expectedUnixTime = "1354320000 +0000";

		var date = new DateTime(2012, 12, 1, 0, 0, 0, DateTimeKind.Utc);
		var unix = UnixTime.FromDateTime(date);

		Assert.AreEqual(unix, expectedUnixTime);

		var dateLocal = date.ToLocalTime();
		var unixFromLocal = UnixTime.FromDateTime(dateLocal);

		Assert.AreEqual(unixFromLocal, expectedUnixTime);

	}

	[TestMethod]
	public void FromDateTime_DateAndTime()
	{
		var expectedUnixTime = "1354369512 +0000";

		var date = new DateTime(2012, 12, 1, 13, 45, 12, DateTimeKind.Utc);
		var unix = UnixTime.FromDateTime(date);

		Assert.AreEqual(unix, expectedUnixTime);

		var dateLocal = date.ToLocalTime();
		var unixFromLocal = UnixTime.FromDateTime(dateLocal);

		Assert.AreEqual(unixFromLocal, expectedUnixTime);
	}

	[TestMethod]
	public void FromDateTime_DateAndTimeWithMilliseconds()
	{
		var expectedUnixTime = "1354369512 +0000";

		var date = new DateTime(2012, 12, 1, 13, 45, 12, 500, DateTimeKind.Utc);
		var unix = UnixTime.FromDateTime(date);

		Assert.AreEqual(unix, expectedUnixTime);

		var dateLocal = date.ToLocalTime();
		var unixFromLocal = UnixTime.FromDateTime(dateLocal);

		Assert.AreEqual(unixFromLocal, expectedUnixTime);
	}
}