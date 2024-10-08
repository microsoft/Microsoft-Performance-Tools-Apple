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
    public sealed class DisplayVsyncIntervalCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(DisplayVsyncIntervalCooker));

        public DisplayVsyncIntervalCooker()
            : base(DataCookerPath)
        {
            this.DisplayVsyncIntervalEvents = new List<DisplayVsyncIntervalEvent>();
        }

        public override string Description => "Display Vsync Interval Event cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(DisplayVsyncIntervalEvent) }));

        [DataOutput]
        public List<DisplayVsyncIntervalEvent> DisplayVsyncIntervalEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            DisplayVsyncIntervalEvents.Add((DisplayVsyncIntervalEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}