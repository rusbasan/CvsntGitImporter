/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace CTC.CvsntGitImporter;

class ImportProgress
{
    private const int WindowSize = 100;
    private readonly int _totalCount;
    private readonly LinkedList<TimeSpan> _windowTimes = new LinkedList<TimeSpan>();

    private int _lastEtaLength = 0;

    public ImportProgress(int totalCount)
    {
        _totalCount = totalCount;
        _windowTimes.AddFirst(TimeSpan.Zero);
    }

    public void Update(TimeSpan elapsed, int count)
    {
        _windowTimes.AddLast(elapsed);
        if (_windowTimes.Count > WindowSize)
            _windowTimes.RemoveFirst();

        var progress = new StringBuilder();
        progress.AppendFormat("({0}%", count * 100 / _totalCount);

        if (count < _totalCount)
        {
            var remaining = CalculateRemaining(count);
            progress.AppendFormat(", {0} remaining", remaining.ToFriendlyDisplay(1));
        }

        progress.Append(")");

        var etaLength = progress.Length;
        if (etaLength < _lastEtaLength)
            progress.Append(new String(' ', _lastEtaLength - etaLength));
        _lastEtaLength = etaLength;

        Console.Out.Write("\rProcessed {0} of {1} commits {2}", count, _totalCount, progress);
    }

    private TimeSpan CalculateRemaining(int count)
    {
        int windowSize = _windowTimes.Count;

        double msTaken = _windowTimes.Last?.Value.TotalMilliseconds - _windowTimes.First?.Value.TotalMilliseconds ?? 0;
        double msRemaining = (msTaken / windowSize) * (_totalCount - count);

        return TimeSpan.FromMilliseconds(msRemaining);
    }
}