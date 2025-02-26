/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Read a log file line by line, tracking the current line number.
/// </summary>
class CvsLogReader : IEnumerable<string>
{
    private readonly string _filename;
    private readonly TextReader _reader;
    private int _lineNumber;

    /// <summary>
    /// Gets the current line number.
    /// </summary>
    public int LineNumber
    {
        get { return _lineNumber; }
    }

    public CvsLogReader(string filename)
    {
        _filename = filename;
    }

    public CvsLogReader(TextReader reader)
    {
        _reader = reader;
        _filename = "<stream>";
    }

    private IEnumerable<string> ReadLines()
    {
        _lineNumber = 0;

        TextReader reader = _reader;
        bool mustDispose = false;
        if (reader == null)
        {
            reader = new StreamReader(_filename, Encoding.Default);
            mustDispose = true;
        }

        try
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                _lineNumber++;
                yield return line;
            }
        }
        finally
        {
            if (mustDispose)
                reader.Dispose();
        }
    }

    public IEnumerator<string> GetEnumerator()
    {
        return ReadLines().GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}