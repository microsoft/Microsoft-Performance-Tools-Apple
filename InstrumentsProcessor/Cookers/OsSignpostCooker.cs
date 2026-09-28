// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK;
using System.Collections.Generic;
using System.Threading;
using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using System;

namespace InstrumentsProcessor.Cookers
{
    public sealed class OsSignpostCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(OsSignpostCooker));

        private readonly Dictionary<long, OsSignpostEvent> _pendingBeginEvents;

        public OsSignpostCooker()
            : base(DataCookerPath)
        {
            this.OsSignpostEvents = new List<OsSignpostEvent>();
            this._pendingBeginEvents = new Dictionary<long, OsSignpostEvent>();
        }

        public override string Description => "OS Signpost cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(OsSignpostEvent) }));

        [DataOutput]
        public List<OsSignpostEvent> OsSignpostEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            var signpostEvent = (OsSignpostEvent)data;
            long signpostId = signpostEvent.SignpostId?.Value ?? 0;
            string eventType = signpostEvent.EventType?.Value ?? "";

            if (eventType == "Begin")
            {
                // Store Begin event for pairing with a future End event.
                _pendingBeginEvents[signpostId] = signpostEvent;
            }
            else if (eventType == "End")
            {
                // EndTime is this End event's own timestamp.
                signpostEvent.EndTime = signpostEvent.Time?.Value ?? default;

                // Pair with the matching Begin event to store begin timestamp.
                if (_pendingBeginEvents.TryGetValue(signpostId, out var beginEvent))
                {
                    signpostEvent.BeginTime = beginEvent.Time?.Value ?? default;
                    _pendingBeginEvents.Remove(signpostId);
                }

                // Output End events only — activities are built from End events.
                OsSignpostEvents.Add(signpostEvent);
            }
            else
            {
                // Single-point "Event" signposts (not Begin/End intervals).
                // Treat them as standalone events with EndTime = their own timestamp.
                signpostEvent.EndTime = signpostEvent.Time?.Value ?? default;
                signpostEvent.BeginTime = signpostEvent.Time?.Value ?? default;
                OsSignpostEvents.Add(signpostEvent);
            }

            return DataProcessingResult.Processed;
        }
    }
}
