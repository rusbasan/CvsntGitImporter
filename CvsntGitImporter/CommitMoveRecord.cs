/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Holds information about commits that need to be moved relative to some final commit, to
/// resolve a tag or a branch point.
/// </summary>
class CommitMoveRecord
{
    private readonly string _tag;
    private readonly ILogger _log;

    private readonly OneToManyDictionary<Commit, FileInfo> _files =
        new OneToManyDictionary<Commit, FileInfo>(CommitComparer.ById);

    private Commit? _finalCommit;

    public CommitMoveRecord(string tag, ILogger log)
    {
        _tag = tag;
        _log = log;
    }

    public Commit? FinalCommit
    {
        get { return _finalCommit; }
        set
        {
            _finalCommit = value;

            // if the final commit is in the list of ones to be moved and does not need splitting, then remove it
            if (value != null && value.Count() == _files[value].Count())
                _files.Remove(value);
        }
    }

    public IEnumerable<Commit> Commits
    {
        get { return _files.Keys; }
    }

    public override string ToString()
    {
        return String.Format("{0}: {1}, {2} commits", _tag, _finalCommit?.CommitId, _files.Count);
    }

    public void AddCommit(Commit commit, IEnumerable<FileInfo> filesToMove)
    {
        _files.AddRange(commit, filesToMove);
    }

    public void AddCommit(Commit commit, FileInfo fileToMove)
    {
        _files.Add(commit, fileToMove);
    }

    public void Apply(IList<Commit> commitStream)
    {
        var finalCommit = _finalCommit;
        int destLocation = finalCommit != null ? commitStream.IndexOf(finalCommit) : -1;
        int searchStart = destLocation;
        var commits = _files.Keys.OrderBy(c => c.Index).ToList();

        Dump();
        _log.WriteLine("Applying:");

        using (_log.Indent())
        {
            // handle in reverse order
            for (int i = commits.Count - 1; i >= 0; i--)
            {
                var commitToMove = commits[i];
                int location = commitStream.IndexOfFromEnd(commitToMove, searchStart);
                if (location < 0)
                {
                    // assume already moved
                    _log.WriteLine("Skip moving {0} after {1}", commitToMove.ConciseFormat,
                        _finalCommit?.ConciseFormat ?? String.Empty);
                    continue;
                }

                // does the commit need splitting?
                var files = _files[commitToMove];
                if (files.Count() < commitToMove.Count())
                {
                    _log.WriteLine("Split {0}", commitToMove.CommitId);

                    using (_log.Indent())
                    {
                        int index = commitToMove.Index;
                        Commit splitCommitNeedMove;
                        Commit splitCommitNoMove;
                        SplitCommit(commitToMove, files, out splitCommitNeedMove, out splitCommitNoMove);

                        commitStream[location] = splitCommitNoMove;
                        commitStream.Insert(location + 1, splitCommitNeedMove);
                        destLocation++;

                        if (_finalCommit == commitToMove)
                            _finalCommit = splitCommitNeedMove;
                        commitToMove = splitCommitNeedMove;

                        // update Commit indices
                        for (int j = location; j < commitStream.Count; j++)
                            commitStream[j].Index = index++;

                        location++;
                    }
                }

                _log.WriteLine("Move {0}({1}) after {2}({3})", commitToMove.ConciseFormat, location,
                    _finalCommit?.ConciseFormat ?? String.Empty, destLocation);
                commitStream.Move(location, destLocation);
                destLocation--;
            }
        }
    }

    private void Dump()
    {
        if (_log.DebugEnabled)
        {
            _log.WriteLine("Commits requiring moving");
            using (_log.Indent())
            {
                foreach (var commit in _files.Keys.OrderBy(c => c.Index))
                {
                    _log.WriteLine("{0}", commit.ConciseFormat);
                    using (_log.Indent())
                    {
                        foreach (var f in _files[commit].OrderBy(f => f.Name))
                            _log.WriteLine("{0} should be at r{1}", f.Name, f.GetRevisionForTag(_tag));
                    }
                }
            }
        }
    }

    private void SplitCommit(Commit parent, IEnumerable<FileInfo> files, out Commit included, out Commit excluded)
    {
        included = new Commit(parent.CommitId + "-1");
        excluded = new Commit(parent.CommitId + "-2");
        _log.WriteLine("New commit {0}", included.CommitId);
        _log.WriteLine("New commit {0}", excluded.CommitId);

        using (_log.Indent())
        {
            foreach (var revision in parent)
            {
                Commit commit = files.Contains(revision.File) ? included : excluded;
                _log.WriteLine("  {0}: add {1}", commit.CommitId, revision.File.Name);
                commit.Add(revision);
                revision.File.UpdateCommit(commit, revision.Revision);
            }
        }
    }
}