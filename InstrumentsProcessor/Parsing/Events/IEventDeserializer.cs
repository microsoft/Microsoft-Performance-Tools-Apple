// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

namespace InstrumentsProcessor.Parsing.Events
{
    public interface IEventDeserializer
    {
        bool CanDeserialize(Schema schema);
        Event Deserialize(XmlNode node, ObjectCache cache, Schema schema);
    }
}
