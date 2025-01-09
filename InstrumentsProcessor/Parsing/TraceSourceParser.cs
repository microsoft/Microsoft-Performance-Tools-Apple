// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using System.Collections.Generic;
using System;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Threading;
using Microsoft.Performance.SDK;
using System.Diagnostics;
using System.Text;
using InstrumentsProcessor.Parsing.Events;

namespace InstrumentsProcessor.Parsing
{
    public sealed class TraceSourceParser
        : SourceParser<Event, ParsingContext, Type>
    {
        private static readonly string TraceQueryResultName = "trace-query-result";
        private static readonly string NodeName = "node";
        private static readonly string SchemaName = "schema";
        private static readonly string RowName = "row";

        private static readonly EventDeserializerProvider eventDeserializerProvider = new EventDeserializerProvider(new IEventDeserializer[]
        {
            new EventDeserializer<TimeProfileEvent>(),
            new EventDeserializer<ThreadStateEvent>(),
            new EventDeserializer<DeviceThermalStateIntervalEvent>(),
            new EventDeserializer<SyscallNameMapEvent>(),
            new EventDeserializer<VirtualMemoryEvent>(),
            new EventDeserializer<SyscallEvent>(),
            new EventDeserializer<PotentialHangEvent>(),
            new EventDeserializer<CpuProfileEvent>(),
            new EventDeserializer<MetalGpuIntervalEvent>(),
            new EventDeserializer<DisplayVsyncIntervalEvent>(),
            new EventDeserializer<CountersProfileEvent>(),
        });

        private ParsingContext context;
        private IEnumerable<IDataSource> dataSources;
        private DataSourceInfo dataSourceInfo;

        public TraceSourceParser(IEnumerable<IDataSource> dataSources)
        {
            context = new ParsingContext();

            // Store the datasources so we can parse them later
            this.dataSources = dataSources;
        }

        // The ID of this Parser.
        public override string Id => nameof(TraceSourceParser);

        // Information about the Data Sources being parsed.
        public override DataSourceInfo DataSourceInfo => this.dataSourceInfo;

        public override void ProcessSource(ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, ILogger logger, IProgress<int> progress, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Timestamp? firstEventTimestamp = null;
            Timestamp? lastEventTimestamp = null;
            int rowCount = 0;

            foreach (IDataSource dataSource in dataSources)
            {
                ProcessDataSource(dataSource, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, progress, cancellationToken);
            }

            long firstEventTimestampNanoseconds = firstEventTimestamp.HasValue ? firstEventTimestamp.Value.ToNanoseconds : 0;
            long lastEventTimestampnanoseconds = lastEventTimestamp.HasValue ? lastEventTimestamp.Value.ToNanoseconds : firstEventTimestampNanoseconds + 1;
            DateTime firstEventWallClockUtc = DateTime.UtcNow;
            dataSourceInfo = new DataSourceInfo(firstEventTimestampNanoseconds, lastEventTimestampnanoseconds, firstEventWallClockUtc);
        }

        public void ProcessDataSource(IDataSource dataSource, ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return;
            }

            XmlReader reader = GetXmlReader(fileDataSource, progress);
            reader.ReadToDescendant(TraceQueryResultName);

            while (reader.Name == TraceQueryResultName)
            {
                if (reader.ReadToDescendant(NodeName))
                {
                    XmlReader subtree = reader.ReadSubtree();
                    ProcessNode(subtree, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, cancellationToken);
                    subtree.Close();
                    reader.Read();
                }
                
                reader.Read();
            }
        }

        public void ProcessNode(XmlReader reader, ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, CancellationToken cancellationToken)
        {
            if (!reader.ReadToDescendant(SchemaName))
            {
                return;
            }

            ObjectCache cache = new ObjectCache();
            Schema schema = (Schema)new XmlSerializer(typeof(Schema)).Deserialize(reader);

            if (!eventDeserializerProvider.TryGetDeserializer(schema, out IEventDeserializer eventDeserializer))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();

            while (reader.Name == RowName)
            {
                XmlNode rowNode = doc.ReadNode(reader);
                Event e = eventDeserializer.Deserialize(rowNode, cache, schema);

                dataProcessor.ProcessDataElement(e, context, cancellationToken);

                if (firstEventTimestamp == null || firstEventTimestamp.Value > e.Timestamp)
                {
                    firstEventTimestamp = e.Timestamp;
                }

                if (lastEventTimestamp == null || lastEventTimestamp.Value < e.Timestamp)
                {
                    lastEventTimestamp = e.Timestamp;
                }
            }
        }

        private static XmlReader GetXmlReader(FileDataSource fileDataSource, IProgress<int> progress)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Create a stream to read the XML file that removes XML declarations, wraps everything in a single root element, and reports progress
            Stream stream = new CompositeStream(
                new List<Stream>
                {
                    new MemoryStream(Encoding.UTF8.GetBytes("<root>")),
                    new FilterStream(
                        new ProgressStream(
                            new FileStream(fileDataSource.FullPath, FileMode.Open, FileAccess.Read),
                            new FileInfo(fileDataSource.FullPath).Length,
                            progress),
                        new List<byte[]> { Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>") }),
                    new MemoryStream(Encoding.UTF8.GetBytes("</root>"))
                });

            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            };

            return XmlReader.Create(stream, settings);
        }
    }
}