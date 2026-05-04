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

                // tagged-backtrace wraps a backtrace element plus additional data (e.g. uint64).
                // Unwrap to the inner backtrace node that contains the frame elements.
                XmlNode framesParent = node;
                if (node.Name == "tagged-backtrace")
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name == "backtrace")
                        {
                            framesParent = child;
                            break;
                        }
                    }
                }

                foreach (XmlNode childNode in framesParent)
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
