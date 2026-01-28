// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class CPU : IPropertyDeserializer
    {
        [CustomDeserialization]
        public int CoreId { get; private set; }

        [CustomDeserialization]
        public string Core { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "CoreId")
            {
                return int.Parse(node.InnerText);
            }
            else if (property.Name == "Core")
            {
                return node.Attributes["fmt"]?.Value;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
