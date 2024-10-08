// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace InstrumentsProcessor.Parsing.Events
{
    public class EventDeserializerProvider
    {
        private readonly IEnumerable<IEventDeserializer> deserializers;

        public EventDeserializerProvider(IEnumerable<IEventDeserializer> deserializers)
        {
            this.deserializers = deserializers;
        }

        public bool TryGetDeserializer(Schema schema, out IEventDeserializer deserializer)
        {
            deserializer = deserializers.FirstOrDefault(d => d.CanDeserialize(schema));
            return deserializer != null;
        }
    }
}
