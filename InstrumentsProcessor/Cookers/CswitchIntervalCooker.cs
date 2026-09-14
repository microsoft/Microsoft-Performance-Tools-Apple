// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK;
using System.Collections.Generic;
using System.Threading;
using System;
using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;

namespace InstrumentsProcessor.Cookers
{
    public sealed class CswitchIntervalCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(CswitchIntervalCooker));

        public CswitchIntervalCooker()
            : base(DataCookerPath)
        {
            this.CswitchIntervalEvents = new List<CswitchIntervalEvent>();
        }

        public override string Description => "Context Switch Interval cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(CswitchIntervalEvent) }));

        [DataOutput]
        public List<CswitchIntervalEvent> CswitchIntervalEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            CswitchIntervalEvents.Add((CswitchIntervalEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
