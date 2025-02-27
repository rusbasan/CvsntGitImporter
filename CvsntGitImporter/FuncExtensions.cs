/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System;
using System.Collections.Generic;

namespace CTC.CvsntGitImporter;

/// <summary>
/// Extension methods on Func.
/// </summary>
static class FuncExtensions
{
    /// <summary>
    /// Memoize a function.
    /// </summary>
    public static Func<T, R> Memoize<T, R>(this Func<T, R> function)
        where T : notnull
    {
        return Memoize<T, R>(function, EqualityComparer<T>.Default);
    }

    /// <summary>
    /// Memoize a function with a custom key comparer.
    /// </summary>
    public static Func<T, R> Memoize<T, R>(this Func<T, R> function, IEqualityComparer<T> comparer)
        where T : notnull
    {
        var lookup = new Dictionary<T, R>(comparer);
        return x =>
        {
            if (lookup.TryGetValue(x, out var result))
            {
                return result;
            }
            else
            {
                result = function(x);
                lookup[x] = result;
                return result;
            }
        };
    }
}