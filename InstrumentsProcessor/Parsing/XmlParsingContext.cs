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
        /// List of all counter names in order as they appear in the info section.
        /// </summary>
        public IReadOnlyList<string> CounterNames { get; private set; }

        public XmlParsingContext()
        {
            ObjectCache = new ObjectCache();
            CounterNames = new List<string>();
        }

        /// <summary>
        /// Sets the counter names for pmc-events based on the info section of the XML.
        /// </summary>
        /// <param name="counterNames">List of counter names in order</param>
        public void SetCounterNames(IList<string> counterNames)
        {
            CounterNames = new List<string>(counterNames);
        }
    }
}
