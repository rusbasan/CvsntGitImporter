/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Manages a list of include/exclude rules.
/// </summary>
class InclusionMatcher
{
    private readonly List<Rule> _rules = new List<Rule>();
    private readonly RegexOptions _regexOptions;

    /// <summary>
    /// The default match value if no rules are added. The default is true.
    /// </summary>
    public bool Default = true;

    /// <summary>
    /// Should this matcher ignore case?
    /// </summary>
    public readonly bool IgnoreCase;

    public InclusionMatcher(bool ignoreCase = false)
    {
        IgnoreCase = ignoreCase;
        _regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
    }

    /// <summary>
    /// Add a rule that includes items if it matches.
    /// </summary>
    /// <exception cref="ArgumentException">the regex pattern is invalid</exception>
    public void AddIncludeRule(string regex)
    {
        if (_rules.Count == 0)
            _rules.Add(MakeRule(".", false));

        _rules.Add(MakeRule(regex, true));
    }

    /// <summary>
    /// Add a rule that excludes items if it matches.
    /// </summary>
    /// <exception cref="ArgumentException">the regex pattern is invalid</exception>
    public void AddExcludeRule(string regex)
    {
        if (_rules.Count == 0)
            _rules.Add(MakeRule(".", true));

        _rules.Add(MakeRule(regex, false));
    }

    /// <summary>
    /// Matches an item.
    /// </summary>
    public bool Match(string item)
    {
        return _rules.Aggregate(Default, (isMatched, rule) => rule.Match(item, isMatched));
    }

    private Rule MakeRule(string regex, bool include)
    {
        return new Rule(new Regex(regex, _regexOptions), include);
    }

    private class Rule
    {
        private readonly Regex _regex;
        private readonly bool _include;

        public Rule(Regex regex, bool include)
        {
            _regex = regex;
            _include = include;
        }

        public bool Match(string item, bool isMatched)
        {
            if (_regex.IsMatch(item))
                return _include;
            else
                return isMatched;
        }

        public override string ToString()
        {
            return String.Format("{0} ({1})", _regex, _include ? "include" : "exclude");
        }
    }
}