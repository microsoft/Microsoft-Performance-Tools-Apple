// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Thread : IPropertyDeserializer
    {
        public Integer ThreadId { get; private set; }
        public Process Process { get; private set; }

        [CustomDeserialization]
        public string ThreadName { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "ThreadName")
            {
                return node.Attributes["fmt"]?.Value ?? string.Empty;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public System.UInt64 ThreadUniqueId()
        {
            System.UInt64 pid = (UInt32)Process.ProcessId.Value;
            UInt32 tid = (UInt32)ThreadId.Value;
            return (pid << 32) | tid;
        }

        public static readonly Thread IdleThread = new Thread
        {
            ThreadId = new Integer(-1),
            Process = Process.IdleProcess,
            ThreadName = "Idle thread"
        };
    }
}