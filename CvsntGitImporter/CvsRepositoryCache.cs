/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.IO;

namespace CTC.CvsntGitImporter;

/// <summary>
/// A cache of CVS repository files.
/// </summary>
class CvsRepositoryCache : ICvsRepository
{
    private readonly string _cacheDir;
    private readonly ICvsRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CvsRepositoryCache"/> class.
    /// </summary>
    /// <exception cref="IOException">failed to create the cache directory</exception>
    public CvsRepositoryCache(string cacheDir, ICvsRepository repository)
    {
        _cacheDir = cacheDir;
        _repository = repository;

        try
        {
            if (!Directory.Exists(_cacheDir))
                Directory.CreateDirectory(_cacheDir);
        }
        catch (UnauthorizedAccessException uae)
        {
            throw new IOException(uae.Message, uae);
        }
    }

    public FileContent GetCvsRevision(FileRevision f)
    {
        var cachedPath = GetCachedRevisionPath(f);
        if (File.Exists(cachedPath))
        {
            var bytes = File.ReadAllBytes(cachedPath);
            return new FileContent(f.File.Name, new FileContentData(bytes, bytes.Length), f.File.IsBinary);
        }
        else
        {
            var contents = _repository.GetCvsRevision(f);
            UpdateCache(cachedPath, contents);
            return contents;
        }
    }

    private static void UpdateCache(string cachedPath, FileContent contents)
    {
        if (contents.Data.Length > int.MaxValue)
            throw new NotSupportedException("Cannot currently cope with files larger than 2 GB");

        Directory.CreateDirectory(Path.GetDirectoryName(cachedPath));
        var tempFile = cachedPath + ".tmp";

        try
        {
            // create temp file in case we're interrupted
            using (var stream = new FileStream(tempFile, FileMode.CreateNew))
            {
                stream.Write(contents.Data.Data, 0, (int)contents.Data.Length);
            }

            File.Move(tempFile, cachedPath);
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    private string GetCachedRevisionPath(FileRevision f)
    {
        var filePath = f.File.Name.Replace('/', '\\');
        return Path.Combine(_cacheDir, filePath, f.Revision.ToString());
    }
}