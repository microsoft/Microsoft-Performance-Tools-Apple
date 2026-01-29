// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class TimestampDelta : IPropertyDeserializer
    {
        public TimestampDelta()
        {
        }

        public TimestampDelta(Microsoft.Performance.SDK.TimestampDelta value)
        {
            Value = value;
        }

        [CustomDeserialization]
        public Microsoft.Performance.SDK.TimestampDelta Value { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return Microsoft.Performance.SDK.TimestampDelta.FromNanoseconds(long.Parse(node.InnerText));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
