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
    public sealed class TrRoleChangeCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(TrRoleChangeCooker));

        public TrRoleChangeCooker()
            : base(DataCookerPath)
        {
            this.TrRoleChangeEvents = new List<TrRoleChangeEvent>();
        }

        public override string Description => "Transition Role Change cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(TrRoleChangeEvent) }));

        [DataOutput]
        public List<TrRoleChangeEvent> TrRoleChangeEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            TrRoleChangeEvents.Add((TrRoleChangeEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
