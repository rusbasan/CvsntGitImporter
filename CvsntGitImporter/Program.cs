/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CTC.CvsntGitImporter.Utils;

namespace CTC.CvsntGitImporter;

class Program
{
    private static Config? _config;
    private static Logger? _log;
    private static BranchStreamCollection? _streams;
    private static IDictionary<string, Commit>? _resolvedTags;

    static int Main(string[] args)
    {
        try
        {
            var switches = new Switches();
            _config = new Config(switches);
            _config.ParseCommandLineSwitches(args);

            if (switches.Help)
            {
                Console.Out.WriteLine(switches.GetHelpText());
                return 0;
            }

            using (_log = new Logger(_config.DebugLogDir, debugEnabled: _config.Debug))
            {
                try
                {
                    if (_config.CreateCvsLog)
                        RunOperation("Download CVS Log",
                            () => Cvs.DownloadCvsLog(_config.CvsLogFileName, _config.Sandbox));

                    RunOperation("Analysis", Analyse);

                    if (_config.DoImport)
                    {
                        RunOperation("Import", Import);

                        if (_config.Repack)
                            RunOperation("Repack", Repack);
                    }
                }
                catch (Exception e)
                {
                    _log.WriteLine("{0}", e);
                    throw;
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }

        return 0;
    }

    private static void RunOperation(string name, Action operation)
    {
        _log?.DoubleRuleOff();
        _log?.WriteLine("{0} started at {1}", name, DateTime.Now);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            operation();
        }
        finally
        {
            stopwatch.Stop();
            _log?.WriteLine("{0} took {1}", name, stopwatch.Elapsed);
            _log?.Flush();
        }
    }

    private static void Analyse()
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) return;

        var parser = new CvsLogParser(config.Sandbox, config.CvsLogFileName, config.BranchMatcher,
            line => config.RemoveAdvertising && config.AdvertisingLines.Any(l => l == line));

        var builder = new CommitBuilder(log, parser.Parse());
        var exclusionFilter = new ExclusionFilter(log, config);

        IEnumerable<Commit> commits = builder.GetCommits()
            .SplitMultiBranchCommits()
            .FilterCommitsOnExcludedBranches()
            .FilterExcludedFiles(exclusionFilter)
            .AddCommitsToFiles()
            .Verify(log)
            .ToListIfNeeded();

        // build lookup of all files
        var allFiles = new FileCollection(parser.Files);
        var includedFiles = new FileCollection(parser.Files.Where(f => config.IncludeFile(f.Name)));

        WriteAllCommitsLog(commits);
        WriteExcludedFileLog(parser);

        var branchResolver = ResolveBranches(commits, includedFiles);
        commits = branchResolver.Commits;

        var tagResolver = ResolveTags(commits, includedFiles);
        commits = tagResolver.Commits;

        WriteTagLog("allbranches.log", branchResolver, parser.ExcludedBranches, config.BranchRename);
        WriteTagLog("alltags.log", tagResolver, parser.ExcludedTags, config.TagRename);
        WriteUserLog("allusers.log", commits);

        var streams = commits.SplitBranchStreams(branchResolver.ResolvedTags);

        // resolve merges
        var mergeResolver = new MergeResolver(log, streams);
        mergeResolver.Resolve();

        WriteBranchLogs(streams);

        // add any "head-only" files
        exclusionFilter.CreateHeadOnlyCommits(config.HeadOnlyBranches, streams, allFiles);

        // store data needed for import
        _streams = streams;
    }

    private static ITagResolver ResolveBranches(IEnumerable<Commit> commits, FileCollection includedFiles)
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) throw new InvalidOperationException("No logger or config");

        ITagResolver branchResolver;
        var autoBranchResolver =
            new AutoBranchResolver(log, includedFiles, config.ContinueOnError, config.NoCommitReordering)
            {
                PartialTagThreshold = config.PartialTagThreshold
            };
        branchResolver = autoBranchResolver;

        // if we're matching branchpoints, resolve those tags first
        if (config.BranchpointRule != null)
        {
            var tagResolver =
                new TagResolver(log, includedFiles, config.ContinueOnError, config.NoCommitReordering)
                {
                    PartialTagThreshold = config.PartialTagThreshold
                };

            var allBranches = includedFiles.SelectMany(f => f.AllBranches).Distinct();
            var rule = config.BranchpointRule;
            var branchpointTags = allBranches.Where(b => rule.IsMatch(b)).Select(b => rule.Apply(b));

            if (!tagResolver.Resolve(branchpointTags, commits))
            {
                var unresolvedTags = tagResolver.UnresolvedTags.OrderBy(i => i);
                log.WriteLine("Unresolved branchpoint tags:");

                using (log.Indent())
                {
                    foreach (var tag in unresolvedTags)
                        log.WriteLine("{0}", tag);
                }
            }

            commits = tagResolver.Commits;
            branchResolver = new ManualBranchResolver(log, autoBranchResolver, tagResolver, config.BranchpointRule);
        }

        // resolve remaining branchpoints
        if (!branchResolver.Resolve(includedFiles.SelectMany(f => f.AllBranches).Distinct(), commits))
        {
            var unresolvedTags = branchResolver.UnresolvedTags.OrderBy(i => i);
            log.WriteLine("Unresolved branches:");

            using (log.Indent())
            {
                foreach (var tag in unresolvedTags)
                    log.WriteLine("{0}", tag);
            }

            if (config.ContinueOnError == false)
            {
                throw new ImportFailedException(String.Format("Unable to resolve all branches to a single commit: {0}",
                    branchResolver.UnresolvedTags.StringJoin(", ")));
            }
            else
            {
                Console.WriteLine(
                    $"Unable to resolve all branches to a single commit: {branchResolver.UnresolvedTags.StringJoin(", ")}");
            }
        }

        return branchResolver;
    }

    private static ITagResolver ResolveTags(IEnumerable<Commit> commits, FileCollection includedFiles)
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) throw new InvalidOperationException("No logger or config");

        var tagResolver = new TagResolver(log, includedFiles, config.ContinueOnError, config.NoCommitReordering)
        {
            PartialTagThreshold = config.PartialTagThreshold
        };

        // resolve tags
        var allTags = includedFiles.SelectMany(f => f.AllTags).Where(t => config.TagMatcher.Match(t));
        if (!tagResolver.Resolve(allTags.Distinct(), commits))
        {
            // ignore branchpoint tags that are unresolved
            var unresolvedTags = tagResolver.UnresolvedTags.OrderBy(i => i);
            log.WriteLine("Unresolved tags:");

            using (log.Indent())
            {
                foreach (var tag in unresolvedTags)
                    log.WriteLine("{0}", tag);
            }

            if (config.ContinueOnError == false)
            {
                throw new ImportFailedException(String.Format("Unable to resolve all tags to a single commit: {0}",
                    unresolvedTags.StringJoin(", ")));
            }
            else
            {
                Console.WriteLine(String.Format("Unable to resolve all tags to a single commit: {0}",
                    unresolvedTags.StringJoin(", ")));
            }
        }

        _resolvedTags = tagResolver.ResolvedTags;
        return tagResolver;
    }

    private static void Import()
    {
        var log = _log;
        var config = _config;
        var streams = _streams;
        var resolvedTags = _resolvedTags;

        if (log == null || config == null || streams == null || resolvedTags == null) return;

        // do the import
        ICvsRepository repository = new CvsRepository(log, config.Sandbox);
        if (config.CvsCache != null)
            repository = new CvsRepositoryCache(config.CvsCache, repository);

        var cvs = new Cvs(repository, config.CvsProcesses);
        var importer = new Importer(log, config, config.Users, streams, resolvedTags, cvs);
        importer.Import();
    }

    private static void Repack()
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) return;

        var git = new GitRepo(log, config.GitDir);
        git.Repack();
    }

    private static void WriteExcludedFileLog(CvsLogParser parser)
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) return;

        if (log.DebugEnabled)
        {
            var files = parser.Files
                .Select(f => f.Name)
                .Where(f => !config.IncludeFile(f))
                .OrderBy(i => i, StringComparer.OrdinalIgnoreCase);

            log.WriteDebugFile("excluded_files.log", files);

            var headOnly = parser.Files
                .Select(f => f.Name)
                .Where(f => config.IsHeadOnly(f))
                .OrderBy(i => i, StringComparer.OrdinalIgnoreCase);

            log.WriteDebugFile("headonly_files.log", headOnly);
        }
    }

    private static void WriteAllCommitsLog(IEnumerable<Commit> commits)
    {
        if (_log?.DebugEnabled != true)
            return;

        using (var log = _log.OpenDebugFile("allcommits.log"))
        {
            foreach (var commit in commits)
            {
                log.WriteLine("Commit {0}", commit.CommitId);
                log.WriteLine(commit.Message);
                log.WriteLine("{0} | {1} | {2}", commit.Branch, commit.Author, commit.Time);

                foreach (var r in commit)
                    log.WriteLine("  {0} r{1}{2}", r.File.Name, r.Revision, r.IsDead ? " (dead)" : "");
                log.WriteLine();
            }
        }
    }

    private static void WriteTagLog(string filename, ITagResolver resolver, IEnumerable<string> excluded,
        Renamer renamer)
    {
        if (_log?.DebugEnabled == true)
        {
            using (var log = _log.OpenDebugFile(filename))
            {
                if (resolver.ResolvedTags().Any())
                {
                    var included = resolver.ResolvedTags()
                        .Select(t => "  " + PrintPossibleRename(t, renamer))
                        .ToList();

                    log.WriteLine("Included:");
                    log.Write(String.Join(Environment.NewLine, included));
                    log.WriteLine();
                    log.WriteLine();
                }

                if (excluded.Any())
                {
                    var excludedDisplay = excluded
                        .Select(t => "  " + PrintPossibleRename(t, renamer))
                        .ToList();

                    log.WriteLine("Excluded:");
                    log.Write(String.Join(Environment.NewLine, excludedDisplay));
                    log.WriteLine();
                }
            }
        }
    }

    private static void WriteBranchLogs(BranchStreamCollection streams)
    {
        var log = _log;

        if (log == null) return;

        foreach (var branch in streams.Branches)
        {
            var filename = String.Format("commits-{0}.log", branch);
            using (var writer = log.OpenDebugFile(filename))
            {
                writer.WriteLine("Branch: {0}", branch);
                writer.WriteLine();

                for (var c = streams[branch]; c != null; c = c.Successor)
                    WriteCommitLog(writer, c);
            }
        }
    }

    private static void WriteUserLog(string filename, IEnumerable<Commit> commits)
    {
        var log = _log;
        var config = _config;

        if (log == null || config == null) return;

        if (!log.DebugEnabled)
            return;

        var allUsers = commits.Select(c => c.Author)
            .Distinct()
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var user = config.Users.GetUser(name);
                if (user.Generated)
                    return name;
                else
                    return String.Format("{0} ({1})", name, user);
            });

        log.WriteDebugFile(filename, allUsers);
    }

    private static string PrintPossibleRename(string tag, Renamer renamer)
    {
        var renamed = renamer.Process(tag);
        if (renamed == tag)
            return tag;
        else
            return String.Format("{0} (renamed to {1})", tag, renamed);
    }

    private static void WriteCommitLog(TextWriter writer, Commit c)
    {
        writer.WriteLine("Commit {0}/{1}", c.CommitId, c.Index);
        writer.WriteLine("{0} by {1}", c.Time, c.Author);

        foreach (var branchCommit in c.Branches)
            writer.WriteLine("Branchpoint for {0}", branchCommit.Branch);

        if (c.MergeFrom != null)
            writer.WriteLine("Merge from {0}/{1} on {2}", c.MergeFrom.CommitId, c.MergeFrom.Index, c.MergeFrom.Branch);

        foreach (var revision in c.OrderBy(r => r.File.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (revision.IsDead)
                writer.Write("  {0} deleted", revision.File.Name);
            else
                writer.Write("  {0} r{1}", revision.File.Name, revision.Revision);

            if (revision.Mergepoint != Revision.Empty)
                writer.WriteLine(" merge from {0} on {1}", revision.Mergepoint,
                    revision.File.GetBranch(revision.Mergepoint));
            else
                writer.WriteLine();
        }

        writer.WriteLine();
    }
}