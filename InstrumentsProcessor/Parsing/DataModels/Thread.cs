// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Thread : IPropertyDeserializer
    {
        // fmt format: "<thread_name> 0x<hex_tid> (<process_name>, pid: <pid>)"
        private static readonly Regex FmtPattern = new Regex(@"^(.+?)\s+\(?0x([0-9a-fA-F]+)\)?\s+\((.+),\s*pid:\s*(\d+)\)\s*$");

        private static XmlNodeDeserializer<Integer> ThreadIdDeserializer = new XmlNodeDeserializer<Integer>();
        [CustomDeserialization]
        public Integer ThreadId { get; private set; }

        [CustomDeserialization]
        public string Name { get; private set; }

        private static XmlNodeDeserializer<Process> ProcessDeserializer = new XmlNodeDeserializer<Process>();
        [CustomDeserialization]
        public Process Process { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "ThreadId")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 1 ? node.ChildNodes[0] : null;

                return ThreadIdDeserializer.Deserialize(propertyNode, context);
            }
            if (property.Name == "Name")
            {
                string fmt = node.Attributes?["fmt"]?.Value;

                if (fmt != null)
                {
                    Match match = FmtPattern.Match(fmt);

                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                return fmt;
            }
            if (property.Name == "Process")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 2 ? node.ChildNodes[1] : null;

                return ProcessDeserializer.Deserialize(propertyNode, context);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
