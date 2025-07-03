// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

namespace InstrumentsProcessor.Parsing.DataModels
{
    /// <summary>
    /// A dictionary that can be indexed by both string counter names and integer indices.
    /// </summary>
    public class CounterValuesDictionary : IReadOnlyDictionary<string, long>
    {
        private readonly Dictionary<string, long> _counterValues = new Dictionary<string, long>();
        private readonly IReadOnlyList<string> _counterNames;

        public CounterValuesDictionary(IReadOnlyList<string> counterNames)
        {
            _counterNames = counterNames ?? new List<string>();
        }

        /// <summary>
        /// Gets a counter value by name
        /// </summary>
        /// <param name="counterName">The name of the counter</param>
        /// <returns>The counter value, or 0 if not found</returns>
        public long this[string counterName]
        {
            get => _counterValues.TryGetValue(counterName, out long value) ? value : 0;
        }

        /// <summary>
        /// Gets a counter value by index
        /// </summary>
        /// <param name="index">The index of the counter</param>
        /// <returns>The counter value, or 0 if not found</returns>
        public long this[int index]
        {
            get
            {
                if (index >= 0 && index < _counterNames.Count)
                {
                    return this[_counterNames[index]];
                }
                return 0;
            }
        }

        /// <summary>
        /// Sets a counter value by name
        /// </summary>
        /// <param name="counterName">The name of the counter</param>
        /// <param name="value">The value to set</param>
        internal void SetValue(string counterName, long value)
        {
            _counterValues[counterName] = value;
        }

        /// <summary>
        /// Clears all counter values
        /// </summary>
        internal void Clear()
        {
            _counterValues.Clear();
        }

        // IReadOnlyDictionary implementation
        public IEnumerable<string> Keys => _counterValues.Keys;
        public IEnumerable<long> Values => _counterValues.Values;
        public int Count => _counterValues.Count;

        public bool ContainsKey(string key) => _counterValues.ContainsKey(key);
        public bool TryGetValue(string key, out long value) => _counterValues.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, long>> GetEnumerator() => _counterValues.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        long IReadOnlyDictionary<string, long>.this[string key] => this[key];
    }
}
