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
    public sealed class TimeProfileCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(TimeProfileCooker));

        private IdleTimeTracker IdleTracker =
            new IdleTimeTracker();

        public TimeProfileCooker()
            : base(DataCookerPath)
        {
            this.TimeProfileEvents = new List<TimeProfileEvent>();
        }

        public override string Description => "Time Profile cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(TimeProfileEvent) }));

        [DataOutput]
        public List<TimeProfileEvent> TimeProfileEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            var tpevent = (TimeProfileEvent)data;
            if ((tpevent.Thread == null) || (tpevent.Core == null))
            {
                return DataProcessingResult.Ignored;
            }

            SynthesizeIdleTime(tpevent);
            TimeProfileEvents.Add(tpevent);

            return DataProcessingResult.Processed;
        }

        private void SynthesizeIdleTime(TimeProfileEvent tpevent)
        {
            Timestamp curr = tpevent.StartTime;
            TimestampDelta idleGap = IdleTracker.Advance(tpevent.Core.CoreId, curr, tpevent.Weight.Value);

            if (idleGap != TimestampDelta.Zero)
            {
                var idleEvent = TimeProfileEvent.MakeIdleEvent(tpevent.Core, curr, idleGap);
                TimeProfileEvents.Add(idleEvent);
            }
        }
    }
}
