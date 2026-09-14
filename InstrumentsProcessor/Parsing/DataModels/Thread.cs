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
        // fmt format: "[<thread_name> ]0x<hex_tid> (<process_name>, pid: <pid>)".
        // The thread name is optional: kernel/system threads are emitted without a
        // symbolic name (e.g. "0x28b1 (Google Chrome Helper, pid: 807)"). The hex tid
        // may also be wrapped in parentheses (e.g. "Main Thread (0x2d69) (...)").
        // Groups: 1 = thread name (optional), 2 = hex tid, 3 = process name, 4 = pid.
        private static readonly Regex FmtPattern = new Regex(@"^(?:(.+?)\s+)?\(?0x([0-9a-fA-F]+)\)?\s+\((.+),\s*pid:\s*(\d+)\)\s*$");

        private static XmlNodeDeserializer<Integer> ThreadIdDeserializer = new XmlNodeDeserializer<Integer>();
        [CustomDeserialization]
        public Integer ThreadId { get; private set; }

        [CustomDeserialization]
        public string Name { get; private set; }

        private static XmlNodeDeserializer<Process> ProcessDeserializer = new XmlNodeDeserializer<Process>();
        [CustomDeserialization]
        public Process Process { get; private set; }

        public Thread() { }
        internal Thread(int tid, string name, Process process)
        {
            ThreadId = new Integer(tid);
            Name = name;
            Process = process;
        }

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
                        string threadName = match.Groups[1].Value;

                        // Name-less threads (e.g. "0x28b1 (Google Chrome Helper, pid: 807)")
                        // have no symbolic name; fall back to the process name so the
                        // column shows a meaningful value instead of the raw hex tid.
                        return !string.IsNullOrEmpty(threadName)
                            ? threadName
                            : match.Groups[3].Value;
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
