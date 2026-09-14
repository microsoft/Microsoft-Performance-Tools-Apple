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
    public sealed class BbSignpostCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(BbSignpostCooker));

        public BbSignpostCooker()
            : base(DataCookerPath)
        {
            this.BbSignpostEvents = new List<BbSignpostEvent>();
        }

        public override string Description => "Browser Benchmark Signpost cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(BbSignpostEvent) }));

        [DataOutput]
        public List<BbSignpostEvent> BbSignpostEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            BbSignpostEvents.Add((BbSignpostEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
