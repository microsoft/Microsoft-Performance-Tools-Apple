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
    public sealed class LifeCyclePeriodCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(LifeCyclePeriodCooker));

        public LifeCyclePeriodCooker()
            : base(DataCookerPath)
        {
            this.LifeCyclePeriodEvents = new List<LifeCyclePeriodEvent>();
        }

        public override string Description => "Life Cycle Period cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(LifeCyclePeriodEvent) }));

        [DataOutput]
        public List<LifeCyclePeriodEvent> LifeCyclePeriodEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            LifeCyclePeriodEvents.Add((LifeCyclePeriodEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
