// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Process : IPropertyDeserializer
    {
        [CustomDeserialization]
        public Integer ProcessId { get; private set;}

        [CustomDeserialization]
        public string Name { get; private set; }

        [CustomDeserialization]
        public String DeviceSession { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "ProcessId")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 1 ? node.ChildNodes[0] : null;

                return Integer.Deserializer.Deserialize(propertyNode, context);
            }
            if (property.Name == "Name")
            {
                return node.Attributes["fmt"]?.Value;
            }
            if (property.Name == "DeviceSession")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 2 ? node.ChildNodes[1] : null;

                return String.Deserializer.Deserialize(propertyNode, context);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static readonly Process IdleProcess = new Process
        {
            ProcessId = new Integer(-1),
            Name = "Idle",
        };
    }
}
