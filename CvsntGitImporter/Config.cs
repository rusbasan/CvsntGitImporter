/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CTC.CvsntGitImporter.Utils;

namespace CTC.CvsntGitImporter;

class Config : IConfig
{
    private readonly Switches _switches;
    private readonly string _debugLogDir;
    private User? _nobody;
    private UserMap? _userMap;
    private List<GitConfigOption>? _gitConfigOptions;
    private readonly InclusionMatcher _fileMatcher = new InclusionMatcher(ignoreCase: true);
    private readonly InclusionMatcher _headOnlyMatcher = new InclusionMatcher(ignoreCase: true) { Default = false };


    public Config(Switches switches)
    {
        _switches = switches;
        _debugLogDir = Path.Combine(Environment.CurrentDirectory, "DebugLogs");

        ObserveCollection(_switches.GitConfigSet, x => AddGitConfigOption(x, add: false));
        ObserveCollection(_switches.GitConfigAdd, x => AddGitConfigOption(x, add: true));

        TagMatcher = new InclusionMatcher(ignoreCase: false);
        TagRename = new Renamer();

        BranchMatcher = new InclusionMatcher(ignoreCase: false);
        BranchRename = new Renamer();

        ObserveCollection(_switches.IncludeFile, x => AddIncludeRule(_fileMatcher, x, include: true));
        ObserveCollection(_switches.ExcludeFile, x => AddIncludeRule(_fileMatcher, x, include: false));
        ObserveCollection(_switches.HeadOnly, x => AddIncludeRule(_headOnlyMatcher, x, include: true));

        ObserveCollection(_switches.IncludeTag, x => AddIncludeRule(TagMatcher, x, include: true));
        ObserveCollection(_switches.ExcludeTag, x => AddIncludeRule(TagMatcher, x, include: false));
        ObserveCollection(_switches.RenameTag, x => AddRenameRule(TagRename, x));

        ObserveCollection(_switches.IncludeBranch, x => AddIncludeRule(BranchMatcher, x, include: true));
        ObserveCollection(_switches.ExcludeBranch, x => AddIncludeRule(BranchMatcher, x, include: false));
        ObserveCollection(_switches.RenameBranch, x => AddRenameRule(BranchRename, x));
    }

    public void ParseCommandLineSwitches(params string[] args)
    {
        _switches.Parse(args);

        try
        {
            if (_switches.BranchpointRule != null)
                BranchpointRule = RenameRule.Parse(_switches.BranchpointRule);
        }
        catch (ArgumentException ae)
        {
            throw new CommandLineArgsException("Invalid branchpoint rule: {0}", ae.Message);
        }

        BranchRename.AddRule(new RenameRule("^MAIN$", MainBranchName));
    }


    #region General config

    /// <summary>
    /// Is debug output and logging enabled?
    /// </summary>
    public bool Debug
    {
        get { return _switches.Debug; }
    }

    /// <summary>
    /// The directory in which debug logs are stored. Never null.
    /// </summary>
    public string DebugLogDir
    {
        get { return _debugLogDir; }
    }

    /// <summary>
    /// Should we actually import the data?
    /// </summary>
    public bool DoImport
    {
        get { return !_switches.NoImport; }
    }

    /// <summary>
    /// Do we need to create the CVS log file?
    /// </summary>
    public bool CreateCvsLog
    {
        get { return _switches.CvsLog == null || !File.Exists(_switches.CvsLog); }
    }

    /// <summary>
    /// The name of the CVS log file. Never null.
    /// </summary>
    public string CvsLogFileName
    {
        get { return _switches.CvsLog ?? Path.Combine(DebugLogDir, "cvs.log"); }
    }

    /// <summary>
    /// The path to the CVS sandbox. Not null.
    /// </summary>
    public string Sandbox
    {
        get { return _switches.Sandbox; }
    }

    /// <summary>
    /// The path to the Git repository to create. Not null.
    /// </summary>
    public string GitDir
    {
        get { return _switches.GitDir; }
    }

    /// <summary>
    /// Gets any configuration options to apply to the new repository.
    /// </summary>
    public IEnumerable<GitConfigOption> GitConfig
    {
        get { return _gitConfigOptions ?? Enumerable.Empty<GitConfigOption>(); }
    }

    /// <summary>
    /// Should we repack the git repository after import?
    /// </summary>
    public bool Repack
    {
        get { return _switches.Repack; }
    }

    /// <summary>
    /// The path to the CVS cache, if specified, otherwise null.
    /// </summary>
    public string? CvsCache
    {
        get { return _switches.CvsCache; }
    }

    /// <summary>
    /// Gets the number of CVS processes to run.
    /// </summary>
    public uint CvsProcesses
    {
        get { return _switches.CvsProcesses ?? (uint)Environment.ProcessorCount; }
    }

    /// <summary>
    /// Reports errors but continues running.
    /// </summary>
    public Boolean ContinueOnError
    {
        get { return _switches.ContinueOnError; }
    }

    /// <summary>
    /// Don't reorder commits.  Depending on how files were tagged or branched, sometimes trying to sort out the
    /// order of commits is futile and can lead to a garbled history or Git errors because when importing the commits
    /// because it's trying to refer to other commits that haven't yet been imported.
    /// </summary>
    public Boolean NoCommitReordering
    {
        get { return _switches.NoCommitReordering; }
    }

    /// <inheritdoc />
    public Boolean RemoveAdvertising => _switches.RemoveAdvertising;

    /// <inheritdoc />
    public Boolean NoLineEndingNormalization => _switches.NoLineEndingNormalization;

    #endregion General config

    #region Users

    /// <summary>
    /// The default domain for user e-mail addresses. Not null.
    /// </summary>
    public string DefaultDomain
    {
        get { return _switches.DefaultDomain ?? Environment.MachineName; }
    }

    /// <summary>
    /// A file containing user mappings, if provided, otherwise null.
    /// </summary>
    public UserMap Users
    {
        get { return _userMap ?? (_userMap = GetUserMap()); }
    }

    /// <summary>
    /// Gets the user to use for creating tags. Never null.
    /// </summary>
    public User Nobody
    {
        get { return _nobody ?? (_nobody = GetNobodyUser()); }
    }

    private User GetNobodyUser()
    {
        var taggerEmail = _switches.NobodyEmail;

        var name = _switches.NobodyName ?? Environment.GetEnvironmentVariable("USERNAME") ?? "nobody";
        name = name.Trim();

        if (taggerEmail == null)
        {
            var emailNamePart = name;
            var spaceIndex = emailNamePart.IndexOf(' ');
            if (spaceIndex > 0)
                emailNamePart = emailNamePart.Remove(spaceIndex);
            taggerEmail = String.Format("{0}@{1}", emailNamePart, DefaultDomain);
        }

        return new User(name, taggerEmail);
    }

    private UserMap GetUserMap()
    {
        var _userMap = new UserMap(this.DefaultDomain);
        _userMap.AddEntry("", this.Nobody);

        if (_switches.UserFile != null)
            _userMap.ParseUserFile(_switches.UserFile);

        return _userMap;
    }

    #endregion Users


    #region File inclusion

    /// <summary>
    /// The branches to import "head-only" files for.
    /// </summary>
    public IEnumerable<string> HeadOnlyBranches
    {
        get { return _switches.HeadOnlyBranches ?? Enumerable.Empty<string>(); }
    }

    /// <summary>
    /// Should a file be imported?
    /// </summary>
    /// <remarks>Excludes files that are "head-only"</remarks>
    public bool IncludeFile(string filename)
    {
        return _fileMatcher.Match(filename) && !_headOnlyMatcher.Match(filename);
    }

    /// <summary>
    /// Is a file a "head-only" file, i.e. one whose head revision only should be imported?
    /// </summary>
    public bool IsHeadOnly(string filename)
    {
        return _fileMatcher.Match(filename) && _headOnlyMatcher.Match(filename);
    }

    #endregion


    #region Tags

    /// <summary>
    /// The default value for PartialTagThreshold.
    /// </summary>
    public const int DefaultPartialTagThreshold = 30;

    /// <summary>
    /// The number of missing files before we declare a tag to be "partial".
    /// </summary>
    public int PartialTagThreshold
    {
        get { return (int)_switches.PartialTagThreshold.GetValueOrDefault(DefaultPartialTagThreshold); }
    }

    /// <summary>
    /// The matcher for tags.
    /// </summary>
    public InclusionMatcher TagMatcher { get; private set; }

    /// <summary>
    /// The renamer for tags.
    /// </summary>
    public Renamer TagRename { get; private set; }

    /// <summary>
    /// The tag to mark imports with.
    /// </summary>
    public string? MarkerTag
    {
        get
        {
            if (_switches.MarkerTag == null)
                return "cvs-import";
            else if (_switches.MarkerTag.Length == 0)
                return null;
            else
                return _switches.MarkerTag;
        }
    }

    #endregion Tags


    #region Branches

    /// <summary>
    /// A rule to translate branch names into branchpoint tag names if specified, otherwise null.
    /// </summary>
    public RenameRule? BranchpointRule { get; private set; }

    /// <summary>
    /// The matcher for branches.
    /// </summary>
    public InclusionMatcher BranchMatcher { get; private set; }

    /// <summary>
    /// The renamer for tags.
    /// </summary>
    public Renamer BranchRename { get; private set; }

    /// <inheritdoc />
    public String MainBranchName => _switches.MainBranchName ?? "main";

    #endregion Branches

    #region Filtering

    /// <inheritdoc />
    public String[] AdvertisingLines { get; } =
    {
        "Committed on the Free edition of March Hare Software CVSNT Server.",
        "Upgrade to CVS Suite for more features and support:",
        "http://march-hare.com/cvsnt/"
    };

    #endregion

    private void AddGitConfigOption(string? x, bool add)
    {
        try
        {
            var option = GitConfigOption.Parse(x ?? String.Empty, add);

            if (_gitConfigOptions == null)
                _gitConfigOptions = new List<GitConfigOption>() { option };
            else
                _gitConfigOptions.Add(option);
        }
        catch (ArgumentException ae)
        {
            throw new CommandLineArgsException("Invalid git option: {0}", ae.Message);
        }
    }

    private void AddIncludeRule(InclusionMatcher matcher, string? pattern, bool include)
    {
        if (pattern == null) return;

        try
        {
            if (include)
                matcher.AddIncludeRule(pattern);
            else
                matcher.AddExcludeRule(pattern);
        }
        catch (ArgumentException)
        {
            throw new CommandLineArgsException("Invalid regex: {0}", pattern);
        }
    }

    private void AddRenameRule(Renamer renamer, string? rule)
    {
        if (rule == null) return;

        try
        {
            renamer.AddRule(RenameRule.Parse(rule));
        }
        catch (ArgumentException ae)
        {
            throw new CommandLineArgsException("Invalid rename rule: {0}", ae.Message);
        }
    }

    private void ObserveCollection(ObservableCollection<string> collection, Action<string?> handler)
    {
        collection.CollectionChanged += (_, e) => handler(e.NewItems?[0] as string);
    }
}