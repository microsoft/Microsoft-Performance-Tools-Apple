// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System;
using System.Xml;
using System.Xml.Linq;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Frame : IPropertyDeserializer
    {
        public Module Module { get; private set; }

        [CustomDeserialization]
        public Function Function { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "Function")
            {
                XElement root = XElement.Parse(node.OuterXml);
                string functionName = root.Attribute("name")?.Value;
                string functionAddress = root.Attribute("addr")?.Value;
                Function function = new Function(functionName, functionAddress);

                return function;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
