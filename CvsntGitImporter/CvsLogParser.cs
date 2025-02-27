/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CTC.CvsntGitImporter;

/// <summary>
/// CVS log file parser.
/// </summary>
class CvsLogParser
{
    private const string LogSeparator = "----------------------------";

    private const string FileSeparator =
        "=============================================================================";

    private static readonly char[] FieldDelimiter = new[] { ';' };

    private readonly string _sandboxPath;
    private readonly CvsLogReader _reader;
    private readonly List<FileInfo> _files = new List<FileInfo>();
    private readonly HashSet<string> _excludedTags = new HashSet<string>();
    private readonly InclusionMatcher _branchMatcher;
    private readonly HashSet<string> _excludedBranches = new HashSet<string>();
    private string? _fullRepoPath;

    /// <summary>
    /// Determines if a commit line should be excluded (due to being advertising, etc.)
    /// </summary>
    private readonly Func<String, Boolean> _excludeLine;

    private CvsLogParser(string sandboxPath, CvsLogReader reader, InclusionMatcher branchMatcher,
        Func<String, Boolean> excludeLine)
    {
        _sandboxPath = sandboxPath;
        _reader = reader;
        _branchMatcher = branchMatcher;
        _excludeLine = excludeLine;
    }

    public CvsLogParser(string sandboxPath, string logFile, InclusionMatcher branchMatcher,
        Func<String, Boolean> excludeLine)
        : this(sandboxPath, new CvsLogReader(logFile), branchMatcher, excludeLine)
    {
    }

    public CvsLogParser(string sandboxPath, TextReader reader, InclusionMatcher branchMatcher,
        Func<String, Boolean> excludeLine)
        : this(sandboxPath, new CvsLogReader(reader), branchMatcher, excludeLine)
    {
    }

    /// <summary>
    /// Gets a list of all the files.
    /// </summary>
    public IEnumerable<FileInfo> Files
    {
        get { return _files; }
    }

    /// <summary>
    /// Gets a list of all the tags that were excluded.
    /// </summary>
    public IEnumerable<string> ExcludedTags
    {
        get { return _excludedTags; }
    }

    /// <summary>
    /// Gets a list of all the branches that were excluded.
    /// </summary>
    public IEnumerable<string> ExcludedBranches
    {
        get { return _excludedBranches; }
    }

    /// <summary>
    /// Parse the log returning a list of the individual commits to the individual files.
    /// </summary>
    public IEnumerable<FileRevision> Parse()
    {
        var state = State.Start;
        FileInfo? currentFile = null;
        Revision revision = Revision.Empty;
        FileRevision? commit = null;

        var repo = GetCvsRepo();

        foreach (var line in _reader)
        {
            var  lineProcessed = false;

            while (lineProcessed == false)
            {
                lineProcessed = true;

                switch (state)
                {
                    case State.Start:
                        if (line.StartsWith("RCS file: "))
                        {
                            currentFile = new FileInfo(ExtractFileName(repo, line));
                            _files.Add(currentFile);
                            state = State.InFileHeader;
                        }

                        break;
                    case State.InFileHeader:
                        if (line == LogSeparator)
                            state = State.ExpectCommitRevision;
                        else if (line == "symbolic names:")
                            state = State.InTags;
                        else if (line.StartsWith("keyword substitution:"))
                        {
                            if (currentFile != null)
                                currentFile.KeywordSubstitution = line.Substring(line.IndexOf(':') + 1).Trim();
                        }

                        break;
                    case State.InTags:
                        if (!line.StartsWith("\t"))
                        {
                            lineProcessed = false;
                            state = State.InFileHeader;
                        }
                        else
                        {
                            var tagMatch = Regex.Match(line, @"^\t(\S+): (\S+)");
                            if (!tagMatch.Success)
                                throw MakeParseException("Invalid tag line: '{0}'", line);

                            var tagName = tagMatch.Groups[1].Value;
                            var tagRevision = Revision.Create(tagMatch.Groups[2].Value);

                            if (tagRevision.IsBranch)
                            {
                                if (_branchMatcher.Match(tagName))
                                    currentFile?.AddBranchTag(tagName, tagRevision);
                                else
                                    _excludedBranches.Add(tagName);
                            }
                            else
                            {
                                currentFile?.AddTag(tagName, tagRevision);
                            }
                        }

                        break;
                    case State.ExpectCommitRevision:
                        if (line.StartsWith("revision "))
                        {
                            revision = Revision.Create(line.Substring(9));
                            state = State.ExpectCommitInfo;
                        }
                        else
                        {
                            throw MakeParseException("Expected revision line, found '{0}'", line);
                        }

                        break;
                    case State.ExpectCommitInfo:
                        commit = currentFile != null ? ParseFields(currentFile, revision, line) : null;
                        state = State.ExpectCommitMessage;
                        break;
                    case State.ExpectCommitMessage:
                        if (line == LogSeparator)
                        {
                            if (commit != null)
                                yield return commit;
                            state = State.ExpectCommitRevision;
                        }
                        else if (line == FileSeparator)
                        {
                            if (commit != null)
                                yield return commit;
                            state = State.Start;
                        }
                        else if (!line.StartsWith("branches:  "))
                        {
                            if (commit != null && _excludeLine(line) == false)
                                commit.AddMessage(line);
                        }

                        break;
                }
            }
        }
    }

    private string GetCvsRepo()
    {
        try
        {
            var repositoryFile = Path.Combine(_sandboxPath, @"CVS\Repository");
            return File.ReadAllText(repositoryFile).Trim() + "/";
        }
        catch (FileNotFoundException)
        {
            throw new ImportFailedException(String.Format("Unable to find CVS sandbox: {0}", _sandboxPath));
        }
        catch (UnauthorizedAccessException uae)
        {
            throw new IOException(uae.Message, uae);
        }
        catch (System.Security.SecurityException se)
        {
            throw new IOException(se.Message, se);
        }
    }

    private string ExtractFileName(String repo, string line)
    {
        var match = Regex.Match(line, @"^RCS file: (.*),v");
        if (!match.Success)
            throw MakeParseException("Invalid RCS file line: '{0}'", line);

        var filepath = match.Groups[1].Value;

        if (_fullRepoPath == null)
        {
            var i = filepath.IndexOf(repo);
            if (i < 0)
                throw MakeParseException("CVS rlog file does not seem to match the repository");
            _fullRepoPath = filepath.Remove(i + repo.Length);
        }

        if (!filepath.StartsWith(_fullRepoPath))
            throw MakeParseException("File path does not seem to match the repository: {0}", filepath);

        return filepath.Substring(_fullRepoPath.Length);
    }

    /// <summary>
    /// Parse a line of the CVS log containing data about a commit.
    /// </summary>
    /// <returns>The commit, or null if the commit is to be ignored</returns>
    private FileRevision ParseFields(FileInfo currentFile, Revision revision, string line)
    {
        var fields = line.Split(FieldDelimiter, StringSplitOptions.RemoveEmptyEntries);
        string? author = null;
        string? commitId = null;
        string? dateStr = null;
        string? mergepointStr = null;
        string? state = null;

        foreach (var field in fields)
        {
            var separator = field.IndexOf(':');
            if (separator <= 0 || separator >= field.Length - 1)
                throw MakeParseException("Invalid field: '{0}'", field);

            var key = field.Remove(separator).Trim();
            var value = field.Substring(separator + 1).Trim();
            switch (key)
            {
                case "author": author = value; break;
                case "commitid": commitId = value; break;
                case "date": dateStr = value; break;
                case "mergepoint": mergepointStr = value; break;
                case "state": state = value; break;
            }
        }

        var time = DateTime.ParseExact(dateStr ?? String.Empty, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal);
        var mergepoint = mergepointStr == null ? Revision.Empty : Revision.Create(mergepointStr);

        return new FileRevision(
            file: currentFile,
            revision: revision,
            mergepoint: mergepoint,
            time: time,
            author: author ?? String.Empty,
            commitId: commitId ?? "",
            isDead: state == "dead");
    }

    private ParseException MakeParseException(string format, params object[] args)
    {
        return new ParseException(String.Format("Line {0}: {1}", _reader.LineNumber, String.Format(format, args)));
    }

    private enum State
    {
        Start = 0,
        InFileHeader,
        InTags,
        ExpectCommitRevision,
        ExpectCommitInfo,
        ExpectCommitMessage,
    }
}