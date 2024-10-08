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
    public sealed class CpuProfileCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(CpuProfileCooker));

        public CpuProfileCooker()
            : base(DataCookerPath)
        {
            this.CpuProfileEvents = new List<CpuProfileEvent>();
        }

        public override string Description => "Cpu Profile cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(CpuProfileEvent) }));

        [DataOutput]
        public List<CpuProfileEvent> CpuProfileEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            CpuProfileEvents.Add((CpuProfileEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
