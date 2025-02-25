/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;

namespace CTC.CvsntGitImporter;

/// <summary>
/// The contents of a file.
/// </summary>
class FileContent
{
    /// <summary>
    /// The file name.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The file's data
    /// </summary>
    public readonly FileContentData Data;

    /// <summary>
    /// Is this a file deletion?
    /// </summary>
    public readonly bool IsDead;

    /// <summary>
    /// Was this flagged as a binary file in CVSNT?
    /// </summary>
    public Boolean IsBinary { get; }

    public FileContent(string path, FileContentData data, Boolean isBinary) : this(path, data, isBinary, false)
    {
    }

    private FileContent(string path, FileContentData data, Boolean isBinary, bool isDead)
    {
        this.Name = path;
        this.Data = data;
        this.IsDead = isDead;
        IsBinary = isBinary;
    }

    public static FileContent CreateDeadFile(string path, Boolean isBinary)
    {
        return new FileContent(path, FileContentData.Empty, isBinary, true);
    }
}