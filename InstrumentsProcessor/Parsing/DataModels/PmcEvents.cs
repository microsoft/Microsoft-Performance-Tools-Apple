// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class PmcEvents : IPropertyDeserializer
    {
        /// <summary>
        /// Dictionary that can be indexed by counter name (string) or index (int) 
        /// </summary>
        [CustomDeserialization]
        public Dictionary<object, long> CounterValues { get; private set; } = new Dictionary<object, long>();

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "CounterValues")
            {
                // Parse the counter values from the node
                string[] array = node.InnerText.Split(' ');
                
                var counterValues = new Dictionary<object, long>();
                
                // Store counter names from context
                var counterNames = context.CounterNames;
                
                // Store values by both name and index
                for (int i = 0; i < array.Length; i++)
                {
                    if (long.TryParse(array[i], out long value))
                    {
                        // Always store by index
                        counterValues[i] = value;
                        
                        // Also store by name if we have counter names
                        if (i < counterNames.Count)
                        {
                            counterValues[counterNames[i]] = value;
                        }
                    }
                }
                
                return counterValues;
            }
            else
            {
                throw new InvalidOperationException($"Unknown property: {property.Name}");
            }
        }
    }
}