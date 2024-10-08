// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class String : IPropertyDeserializer
    {
        [CustomDeserialization]
        public string Value { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return node.Attributes["fmt"]?.Value ?? string.Empty;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}