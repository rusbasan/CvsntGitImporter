/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Build commits from the CVS log.
/// </summary>
class CommitBuilder
{
    private readonly ILogger _log;
    private readonly IEnumerable<FileRevision> _fileRevisions;

    public CommitBuilder(ILogger log, IEnumerable<FileRevision> fileRevisions)
    {
        _log = log;
        _fileRevisions = fileRevisions;
    }

    /// <summary>
    /// Get all the commits in a CVS log ordered by date.
    /// </summary>
    public IEnumerable<Commit> GetCommits()
    {
        var lookup = new Dictionary<string, Commit>();
        using (var commitsByMessage = new CommitsByMessage(_log))
        {
            foreach (var revision in _fileRevisions)
            {
                if (revision.IsAddedOnAnotherBranch)
                {
                    revision.File.BranchAddedOn = GetBranchAddedOn(revision.Message);
                }
                else if (revision.CommitId.Length == 0)
                {
                    commitsByMessage.Add(revision);
                }
                else
                {
                    Commit? commit;
                    if (lookup.TryGetValue(revision.CommitId, out commit))
                    {
                        commit.Add(revision);
                    }
                    else
                    {
                        commit = new Commit(revision.CommitId) { revision };
                        lookup.Add(commit.CommitId, commit);
                    }
                }
            }

            return lookup.Values.Concat(commitsByMessage.Resolve()).OrderBy(c => c.Time).ToList();
        }
    }

    private static string GetBranchAddedOn(string message)
    {
        var match = Regex.Match(message, @"initially added on branch (\S+)\.");
        if (!match.Success)
        {
            throw new ArgumentException(String.Format(
                "Trying to extract branch name from message, but message in incorrect format: '{0}'",
                message));
        }

        return match.Groups[1].Value;
    }

    private class CommitsByMessage : IDisposable
    {
        private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(10);

        private readonly TextWriter _debugLog;

        private readonly OneToManyDictionary<string, FileRevision> _revisions =
            new OneToManyDictionary<string, FileRevision>();

        private int _nextCommitId;
        private bool _isDisposed = false;

        public CommitsByMessage(ILogger log)
        {
            if (log.DebugEnabled)
                _debugLog = log.OpenDebugFile("created_commits.log");
            else
                _debugLog = TextWriter.Null;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _debugLog.Close();
            }

            _isDisposed = true;
        }


        public void Add(FileRevision revision)
        {
            _revisions.Add(revision.Message, revision);
        }

        public IEnumerable<Commit> Resolve()
        {
            foreach (var msg in _revisions.Keys)
            {
                var revisionList = new List<FileRevision>(_revisions[msg]);
                revisionList.Sort((a, b) => DateTime.Compare(a.Time, b.Time));
                int start = 0;
                var lastTime = revisionList[0].Time;

                for (int i = 1; i < revisionList.Count; i++)
                {
                    if (revisionList[i].Time - lastTime > MaxInterval)
                    {
                        yield return MakeCommit(revisionList, start, i);
                        start = i;
                    }
                }

                if (start < revisionList.Count)
                    yield return MakeCommit(revisionList, start, revisionList.Count);
            }
        }

        private Commit MakeCommit(List<FileRevision> revisions, int start, int end)
        {
            var commit = new Commit(MakeCommitId(revisions[start]));

            for (int i = start; i < end; i++)
            {
                commit.Add(revisions[i]);
                // need to wait for the first revision to be added, so that the commit message is set
                if (i == start)
                    _debugLog.WriteLine("Commit {0}{1}{2}", commit.CommitId, Environment.NewLine, commit.Message);
                _debugLog.WriteLine("  {0} r{1}", revisions[i].File.Name, revisions[i].Revision);
            }

            _debugLog.WriteLine();
            return commit;
        }

        private string MakeCommitId(FileRevision r)
        {
            return String.Format("{0:yyMMdd}-{1}-{2}", r.Time, r.Author, _nextCommitId++);
        }
    }
}