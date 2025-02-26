/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTC.CvsntGitImporter;

/// <summary>
/// CVS interface
/// </summary>
class CvsRepository : ICvsRepository
{
    private readonly ILogger _log;
    private readonly string _sandboxPath;
    private readonly Task _ensureAllDirectories;

    public CvsRepository(ILogger log, string sandboxPath)
    {
        _log = log;
        _sandboxPath = sandboxPath;

        // start the CVS update command that ensures that all empty directories are created
        _ensureAllDirectories = EnsureAllDirectories();
    }

    public FileContent GetCvsRevision(FileRevision f)
    {
        _ensureAllDirectories.Wait();

        InvokeCvs("-f", "-Q", "update", "-r" + f.Revision.ToString(), f.File.Name);

        var dataPath = Path.Combine(_sandboxPath, f.File.Name.Replace('/', '\\'));
        return new FileContent(f.File.Name, new FileContentData(File.ReadAllBytes(dataPath)), f.File.IsBinary);
    }

    /// <summary>
    /// Create all directories, including empty ones.
    /// </summary>
    private async Task EnsureAllDirectories()
    {
        await Task.Factory.StartNew(() => InvokeCvs("-f", "-q", "update", "-d"));
    }

    private void InvokeCvs(params string[] args)
    {
        var quotedArguments = String.Join(" ", args.Select(a => a.Contains(' ') ? String.Format("\"{0}\"", a) : a));

        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "cvs.exe",
                Arguments = quotedArguments,
                WorkingDirectory = _sandboxPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default,
                CreateNoWindow = true,
            },
        };

        var error = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                error.AppendLine(e.Data);
        };

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();

        if (error.Length > 0 || process.ExitCode != 0)
        {
            _log.DoubleRuleOff();
            _log.WriteLine("Cvs command failed");
            _log.WriteLine("Command: cvs {0}", quotedArguments);

            if (error.Length > 0)
            {
                _log.RuleOff();
                _log.WriteLine("Error:");
                _log.WriteLine("{0}", error);
            }

            if (output.Length > 0)
            {
                _log.RuleOff();
                _log.WriteLine("Output:");
                _log.WriteLine("{0}", output);
            }
        }

        if (process.ExitCode != 0)
            throw new CvsException(String.Format("CVS exited with exit code {0} (see debug log for details)",
                process.ExitCode));
    }
}