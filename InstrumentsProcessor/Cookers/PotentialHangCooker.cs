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
    public sealed class PotentialHangCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(PotentialHangCooker));

        public PotentialHangCooker()
            : base(DataCookerPath)
        {
            this.PotentialHangEvents = new List<PotentialHangEvent>();
        }

        public override string Description => "Syscall Name Map cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(PotentialHangEvent) }));

        [DataOutput]
        public List<PotentialHangEvent> PotentialHangEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            PotentialHangEvents.Add((PotentialHangEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
