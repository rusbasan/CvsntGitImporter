/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;

namespace CTC.CvsntGitImporter;

/// <summary>
/// An exception that occurred while parsing.
/// </summary>
[Serializable]
class ParseException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <cref>CvsGitConverter.ParseException</cref> class.
	/// </summary>
	public ParseException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <cref>CvsGitConverter.ParseException</cref> class with a
	/// specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public ParseException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <cref>CvsGitConverter.ParseException</cref> class with a
	/// specified error message and a reference to the inner exception that is the cause of this exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
	public ParseException(string message, Exception inner)
		: base(message, inner)
	{
	}
}