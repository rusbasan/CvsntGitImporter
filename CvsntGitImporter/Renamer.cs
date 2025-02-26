/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Collection of rename rules to rename tags and branches. The rules are processed in the order in which
/// they were added and processing stops as soon as a rule matches.
/// </summary>
class Renamer
{
    private readonly List<RenameRule> _rules = new List<RenameRule>();

    /// <summary>
    /// Adds a renaming rule.
    /// </summary>
    public void AddRule(RenameRule rule)
    {
        _rules.Add(rule);
    }

    /// <summary>
    /// Process a name, renaming it if it matches a rule.
    /// </summary>
    public string Process(string name)
    {
        var match = _rules.FirstOrDefault(r => r.IsMatch(name));
        if (match == null)
            return name;
        else
            return match.Apply(name);
    }
}