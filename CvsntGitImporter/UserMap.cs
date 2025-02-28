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
/// Maps CVS users to proper names and e-mail addresses.
/// </summary>
/// <remarks>The file is a tab separated file with three columns: CVS user name, real name and e-mail address</remarks>
class UserMap
{
    private readonly Dictionary<string, User> _map = new Dictionary<string, User>();
    private readonly string _defaultDomain;

    public UserMap(string defaultDomain)
    {
        _defaultDomain = defaultDomain;
    }

    public User GetUser(string cvsName)
    {
        if (_map.TryGetValue(cvsName, out var result))
            return result;

        result = CreateDefaultUser(cvsName);
        _map[cvsName] = result;
        return result;
    }

    /// <summary>
    /// Add a single entry.
    /// </summary>
    public void AddEntry(string cvsName, User user)
    {
        _map[cvsName] = user;
    }

    /// <summary>
    /// Parse a user file.
    /// </summary>
    /// <exception cref="IOException">if an error occurs reading the file</exception>
    public void ParseUserFile(string filename)
    {
        try
        {
            using (var reader = new StreamReader(filename, Encoding.UTF8))
            {
                ParseUserFile(reader, filename);
            }
        }
        catch (UnauthorizedAccessException uae)
        {
            throw new IOException(uae.Message, uae);
        }
        catch (System.Security.SecurityException se)
        {
            throw new IOException(se.Message, se);
        }
    }

    /// <summary>
    /// Parse a user file.
    /// </summary>
    /// <exception cref="IOException">if an error occurs reading the file</exception>
    /// <remarks>This overload exists primarily for the user by testcode, where we're reading a test file
    /// from resources.</remarks>
    public void ParseUserFile(TextReader reader, string filename)
    {
        try
        {
            int lineNumber = 0;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                line = line.Trim();
                if (line.Length == 0)
                    continue;

                var parts = line.Split('\t');
                if (parts.Length != 3)
                    throw new IOException(String.Format("{0}({1}): Invalid format in user file", filename, lineNumber));

                var cvsName = parts[0].Trim();
                if (_map.ContainsKey(cvsName))
                    throw new IOException(String.Format("{0}({1}): User {2} appears twice", filename, lineNumber,
                        cvsName));

                var user = new User(parts[1].Trim(), parts[2].Trim());
                _map[cvsName] = user;
            }
        }
        catch (UnauthorizedAccessException uae)
        {
            throw new IOException(uae.Message, uae);
        }
        catch (System.Security.SecurityException se)
        {
            throw new IOException(se.Message, se);
        }
    }


    private User CreateDefaultUser(string cvsName)
    {
        return new User(cvsName, String.Format("{0}@{1}", cvsName.Replace(' ', '_'), _defaultDomain), generated: true);
    }
}