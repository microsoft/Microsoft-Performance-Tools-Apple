// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using System;
using System.Collections.Generic;
using System.Threading;

namespace InstrumentsProcessor.Cookers
{
    public sealed class ActivityMonitorSystemCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(ActivityMonitorSystemCooker));

        public ActivityMonitorSystemCooker() : base(DataCookerPath)
        {
            ActivityMonitorSystemEvents = new List<ActivityMonitorSystemEvent>();
        }

        public override string Description => "Activity Monitor system-wide sampled resource usage.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(ActivityMonitorSystemEvent) }));

        [DataOutput]
        public List<ActivityMonitorSystemEvent> ActivityMonitorSystemEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data, ParsingContext context, CancellationToken cancellationToken)
        {
            ActivityMonitorSystemEvents.Add((ActivityMonitorSystemEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}
