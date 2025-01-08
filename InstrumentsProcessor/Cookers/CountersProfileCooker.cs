﻿// Copyright (c) Microsoft Corporation.
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
    public sealed class CountersProfileCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(CountersProfileCooker));

        public CountersProfileCooker()
            : base(DataCookerPath)
        {
            this.CountersProfileEvent = new List<CountersProfileEvent>();
        }

        public override string Description => "Counters Profile cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(CountersProfileEvent) }));

        [DataOutput]
        public List<CountersProfileEvent> CountersProfileEvent { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            CountersProfileEvent.Add((CountersProfileEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
