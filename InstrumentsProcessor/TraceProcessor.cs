// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using System;

namespace InstrumentsProcessor
{
    public sealed class TraceProcessor
        : CustomDataProcessorWithSourceParser<Event, ParsingContext, Type>
    {
        public TraceProcessor(
            ISourceParser<Event, ParsingContext, Type> sourceParser,
            ProcessorOptions options,
            IApplicationEnvironment applicationEnvironment,
            IProcessorEnvironment processorEnvironment)
            : base(sourceParser, options, applicationEnvironment, processorEnvironment)
        {
        }
    }
}