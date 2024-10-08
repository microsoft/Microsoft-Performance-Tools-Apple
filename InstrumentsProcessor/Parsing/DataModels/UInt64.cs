// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class UInt64 : IPropertyDeserializer
    {
        [CustomDeserialization]
        public ulong Value { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return System.UInt64.Parse(node.InnerText);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}