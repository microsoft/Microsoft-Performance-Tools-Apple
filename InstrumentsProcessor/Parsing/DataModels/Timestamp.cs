// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Timestamp : IPropertyDeserializer
    {
        [CustomDeserialization]
        public Microsoft.Performance.SDK.Timestamp Value { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return Microsoft.Performance.SDK.Timestamp.FromNanoseconds(long.Parse(node.InnerText));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
