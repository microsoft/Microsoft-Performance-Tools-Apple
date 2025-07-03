// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace InstrumentsProcessor.Parsing
{
    /// <summary>
    /// XML-specific parsing context that contains ObjectCache and additional metadata
    /// for parsing XML files, including counter dictionaries for pmc-events naming.
    /// </summary>
    public class XmlParsingContext
    {
        /// <summary>
        /// The object cache for storing and retrieving objects during parsing.
        /// </summary>
        public ObjectCache ObjectCache { get; }

        /// <summary>
        /// Dictionary containing counter names for pmc-events, if available.
        /// Key is the index, value is the counter name.
        /// </summary>
        public IDictionary<int, string> CounterNames { get; private set; }

        public XmlParsingContext()
        {
            ObjectCache = new ObjectCache();
            CounterNames = new Dictionary<int, string>();
        }

        /// <summary>
        /// Sets the counter names for pmc-events based on the info section of the XML.
        /// </summary>
        /// <param name="counterNames">List of counter names in order</param>
        public void SetCounterNames(IList<string> counterNames)
        {
            CounterNames = new Dictionary<int, string>();
            for (int i = 0; i < counterNames.Count; i++)
            {
                CounterNames[i] = counterNames[i];
            }
        }
    }
}
