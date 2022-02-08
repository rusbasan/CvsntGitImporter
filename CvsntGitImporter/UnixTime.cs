/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2022 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Diagnostics;

namespace CTC.CvsntGitImporter
{
	static class UnixTime
	{
		/// <summary>
		/// Convert a .NET DateTime to a Unix time string.
		/// </summary>
		public static string FromDateTime(DateTime dateTime)
		{
			Debug.Assert(
				dateTime.Kind != DateTimeKind.Unspecified,
				"Unspecified times lead to inconsistent results.");

			return String.Format("{0} +0000", (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
		}
	}
}