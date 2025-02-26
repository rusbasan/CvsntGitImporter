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
/// Logging.
/// </summary>
class Logger : ILogger, IDisposable
{
    private bool _isDisposed = false;
    private readonly string _logDir;
    private readonly TextWriter _writer;

    private const int IndentCount = 2;
    private string _currentIndent = "";
    private readonly string _singleIndent = new string(' ', IndentCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="directoryName">The directory to store log files in.</param>
    /// <exception cref="IOException">there was an error opening the log file</exception>
    public Logger(string directoryName, bool debugEnabled = false)
    {
        _logDir = directoryName;
        DebugEnabled = debugEnabled;
        Directory.CreateDirectory(directoryName);

        try
        {
            var filename = GetLogFilePath("import.log");
            _writer = new StreamWriter(filename, false, Encoding.UTF8);

            Console.CancelKeyPress += (_, e) => _writer.Close();
        }
        catch (System.Security.SecurityException se)
        {
            throw new IOException(se.Message, se);
        }
        catch (UnauthorizedAccessException uae)
        {
            throw new IOException(uae.Message, uae);
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _writer.Close();
        }

        _isDisposed = true;
    }

    public bool DebugEnabled { get; set; }

    public IDisposable Indent()
    {
        _currentIndent += _singleIndent;
        return new Indenter(this);
    }

    private void Outdent()
    {
        if (_currentIndent.Length > 0)
            _currentIndent = _currentIndent.Substring(0, _currentIndent.Length - 2);
    }

    public void WriteLine()
    {
        _writer.WriteLine();
    }

    public void WriteLine(string line)
    {
        _writer.Write(_currentIndent);
        _writer.WriteLine(line);
    }

    public void WriteLine(string format, params object[] args)
    {
        _writer.Write(_currentIndent);
        _writer.WriteLine(format, args);
    }

    public void RuleOff()
    {
        _writer.WriteLine("-------------------------------------------------------------------------------");
    }

    public void DoubleRuleOff()
    {
        _writer.WriteLine("===============================================================================");
    }

    public void Flush()
    {
        _writer.Flush();
    }


    public void WriteDebugFile(string filename, IEnumerable<string> lines)
    {
        if (DebugEnabled)
        {
            var logPath = GetLogFilePath(filename);
            File.WriteAllLines(logPath, lines);
        }
    }

    public TextWriter OpenDebugFile(string filename)
    {
        if (DebugEnabled)
        {
            return new StreamWriter(GetLogFilePath(filename), append: false, encoding: Encoding.UTF8);
        }
        else
        {
            return TextWriter.Null;
        }
    }


    private string GetLogFilePath(string filename)
    {
        return Path.Combine(_logDir, filename);
    }


    private class Indenter : IDisposable
    {
        private bool _isDisposed = false;
        private readonly Logger _logger;

        public Indenter(Logger logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _logger.Outdent();
            }

            _isDisposed = true;
        }
    }
}