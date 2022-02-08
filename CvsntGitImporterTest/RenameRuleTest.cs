/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2022 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CTC.CvsntGitImporter.TestCode
{
	/// <summary>
	/// Unit tests for the RenameRule class.
	/// </summary>
	[TestClass]
	public class RenameRuleTest
	{
		#region Parse

		[TestMethod]
		public void Parse_ValidString()
		{
			var ruleString = @"^(x+) / $1x";
			var rule = RenameRule.Parse(ruleString);

			Assert.IsTrue(rule.IsMatch("xx_"));
			Assert.AreEqual("xxx_", rule.Apply("xx_"));
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void Parse_InvalidString()
		{
			var ruleString = @"(.+) | $1x";
			var rule = RenameRule.Parse(ruleString);
		}

		[TestMethod]
		[ExpectedException(typeof(RegexParseException))]
		public void Parse_InvalidRegex()
		{
			var ruleString = @"(.**) / $1x";
			var rule = RenameRule.Parse(ruleString);
		}

		#endregion Parse
	}
}