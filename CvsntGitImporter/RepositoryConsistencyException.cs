/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Thrown when the CVS repository is inconsistent.
/// </summary>
[Serializable]
class RepositoryConsistencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <cref>CvsGitConverter.RepositoryConsistencyException</cref> class.
    /// </summary>
    public RepositoryConsistencyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <cref>CvsGitConverter.RepositoryConsistencyException</cref> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RepositoryConsistencyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <cref>CvsGitConverter.RepositoryConsistencyException</cref> class with a
    /// specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public RepositoryConsistencyException(string message, Exception inner)
        : base(message, inner)
    {
    }
}