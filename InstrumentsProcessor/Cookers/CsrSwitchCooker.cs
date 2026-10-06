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
    public sealed class CsrSwitchCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(CsrSwitchCooker));

        public CsrSwitchCooker()
            : base(DataCookerPath)
        {
            this.CsrSwitchEvents = new List<CsrSwitchEvent>();
        }

        public override string Description => "CSR Switch On/Off cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(CsrSwitchEvent) }));

        [DataOutput]
        public List<CsrSwitchEvent> CsrSwitchEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            CsrSwitchEvents.Add((CsrSwitchEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
