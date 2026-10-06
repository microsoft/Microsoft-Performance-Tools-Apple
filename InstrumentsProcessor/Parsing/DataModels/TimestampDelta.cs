// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class TimestampDelta : IPropertyDeserializer
    {
        [CustomDeserialization]
        public Microsoft.Performance.SDK.TimestampDelta Value { get; private set; }

        public TimestampDelta() { }
        internal TimestampDelta(long nanoseconds) { Value = Microsoft.Performance.SDK.TimestampDelta.FromNanoseconds(nanoseconds); }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                if (long.TryParse(node.InnerText, out long nanoseconds))
                {
                    return Microsoft.Performance.SDK.TimestampDelta.FromNanoseconds(nanoseconds);
                }

                return Microsoft.Performance.SDK.TimestampDelta.Zero;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
