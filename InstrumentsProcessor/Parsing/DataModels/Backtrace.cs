// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Backtrace : IPropertyDeserializer
    {
        private static XmlNodeDeserializer<Frame> FrameDeserializer = new XmlNodeDeserializer<Frame>();

        [CustomDeserialization]
        public IReadOnlyList<Frame> Frames { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Frames")
            {
                List<Frame> frames = new List<Frame>();

                foreach (XmlNode childNode in node)
                {
                    frames.Add(FrameDeserializer.Deserialize(childNode, context));
                }

                return frames;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
