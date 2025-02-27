/*
 * John Hall <john.hall@camtechconsultants.com>
 * © 2013-2025 Cambridge Technology Consultants Ltd.
 */

using System.Collections.Generic;
using System.Linq;

namespace CTC.CvsntGitImporter;

/// <summary>
/// A dictionary that maps a key to a list of values.
/// </summary>
/// <remarks>This implementation differs from the standard dictionary in that it is much more forgiving of keys
/// that do not exist. For example, the indexer returns an empty list if a value does not exist.</remarks>
class OneToManyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, HashSet<TValue>> _dict;

    public OneToManyDictionary() : this(EqualityComparer<TKey>.Default)
    {
    }

    public OneToManyDictionary(IEqualityComparer<TKey> comparer)
    {
        _dict = new Dictionary<TKey, HashSet<TValue>>(comparer);
    }

    /// <summary>
    /// Gets or sets the list of items for a key. When setting, any existing values are
    /// replaced rather than appended to.
    /// </summary>
    public IEnumerable<TValue> this[TKey key]
    {
        get
        {
            if (_dict.TryGetValue(key, out var values))
                return values;
            else
                return Enumerable.Empty<TValue>();
        }
        set { _dict[key] = new HashSet<TValue>(value); }
    }

    /// <summary>
    /// Gets the list of keys in the dictionary.
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get { return _dict.Keys; }
    }

    /// <summary>
    /// Gets the number of keys in the collection.
    /// </summary>
    public int Count
    {
        get { return _dict.Count; }
    }

    /// <summary>
    /// Add a value for a key.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        if (_dict.TryGetValue(key, out var values))
            values.Add(value);
        else
            _dict[key] = new HashSet<TValue>() { value };
    }

    /// <summary>
    /// Add a list of values for a key.
    /// </summary>
    public void AddRange(TKey key, IEnumerable<TValue> values)
    {
        if (_dict.TryGetValue(key, out var existingValues))
            existingValues.AddRange(values);
        else
            _dict[key] = new HashSet<TValue>(values);
    }

    /// <summary>
    /// Does the collection contain a key?
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        return _dict.ContainsKey(key);
    }

    /// <summary>
    /// Remove a key from the collection.
    /// </summary>
    public void Remove(TKey key)
    {
        _dict.Remove(key);
    }
}