/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Text.RegularExpressions;

namespace CTC.CvsntGitImporter;

/// <summary>
/// A rename rule - a regular expression and a replacement pattern.
/// </summary>
class RenameRule
{
    private readonly Regex _pattern;
    private readonly string _replacement;

    public RenameRule(Regex pattern, string replacement)
    {
        this._pattern = pattern;
        this._replacement = replacement;
    }

    public RenameRule(string pattern, string replacement) : this(new Regex(pattern), replacement)
    {
    }

    /// <summary>
    /// Parse a rename rule, where the pattern and replacement are separated by a slash.
    /// This is the form it is passed in on the command-line.
    /// </summary>
    /// <exception cref="ArgumentException">the format of the rule is invalid</exception>
    public static RenameRule Parse(string ruleString)
    {
        var parts = ruleString.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException(String.Format("The string is not in the expected format: {0}", ruleString));

        var regex = new Regex(parts[0].Trim());
        return new RenameRule(regex, parts[1].Trim());
    }

    /// <summary>
    /// Does this rule match an input string>
    /// </summary>
    public bool IsMatch(string input)
    {
        return _pattern.IsMatch(input);
    }

    /// <summary>
    /// Apply the rule.
    /// </summary>
    public string Apply(string input)
    {
        return _pattern.Replace(input, _replacement);
    }

    public override string ToString()
    {
        return string.Format("{0} -> {1}", _pattern.ToString(), _replacement);
    }
}