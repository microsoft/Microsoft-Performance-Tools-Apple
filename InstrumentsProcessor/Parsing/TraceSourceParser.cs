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
using System.Linq;
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
        private static readonly string InfoName = "info";

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
            new EventDeserializer<AneHwIntervalEvent>(),
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
            Timestamp? firstEventTimestamp = null;
            Timestamp? lastEventTimestamp = null;
            DateTime? recordingStartUtc = null;

            foreach (IDataSource dataSource in dataSources)
            {
                ProcessDataSource(dataSource, ref firstEventTimestamp, ref lastEventTimestamp, ref recordingStartUtc, dataProcessor, progress, cancellationToken);
            }

            long firstEventTimestampNanoseconds = firstEventTimestamp.HasValue ? firstEventTimestamp.Value.ToNanoseconds : 0;
            long lastEventTimestampnanoseconds = lastEventTimestamp.HasValue ? lastEventTimestamp.Value.ToNanoseconds : firstEventTimestampNanoseconds + 1;

            // Anchor the trace's wall-clock to the recording's actual start time (from info/summary/start-date in the xctrace XML)
            // rather than to load time. Using DateTime.UtcNow here causes WPA's session timeline to be offset by the elapsed time
            // between recording and loading, misaligning this trace with other simultaneously-collected sources (e.g. Perfetto).
            DateTime firstEventWallClockUtc = recordingStartUtc ?? DateTime.UtcNow;
            dataSourceInfo = new DataSourceInfo(firstEventTimestampNanoseconds, lastEventTimestampnanoseconds, firstEventWallClockUtc);
        }

        public void ProcessDataSource(IDataSource dataSource, ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp, ref DateTime? recordingStartUtc,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return;
            }

            XmlReader reader = GetXmlReader(fileDataSource, progress);

            // Create the XML parsing context
            XmlParsingContext xmlContext = new XmlParsingContext();

            // Read past root elements
            reader.Read();
            reader.Read();
            
            if (reader.Name == InfoName)
            {
                // Try to parse the info section first to extract counter names and the recording wall-clock anchor
                ParseInfoSection(reader, xmlContext);

                // Aggregate the earliest recording start across data sources, so multi-file loads still produce a coherent anchor.
                if (xmlContext.RecordingStartUtc.HasValue &&
                    (!recordingStartUtc.HasValue || xmlContext.RecordingStartUtc.Value < recordingStartUtc.Value))
                {
                    recordingStartUtc = xmlContext.RecordingStartUtc.Value;
                }
            }
            
            if (reader.Name != TraceQueryResultName)
            {
                reader.ReadToDescendant(TraceQueryResultName);
            }

            while (reader.Name == TraceQueryResultName)
            {
                if (reader.ReadToDescendant(NodeName))
                {
                    XmlReader subtree = reader.ReadSubtree();
                    ProcessNode(subtree, xmlContext, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, cancellationToken);
                    subtree.Close();
                    reader.Read();
                }
                
                reader.Read();
            }
        }

        public void ProcessNode(XmlReader reader, XmlParsingContext xmlContext, 
            ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, CancellationToken cancellationToken)
        {
            if (!reader.ReadToDescendant(SchemaName))
            {
                return;
            }

            xmlContext.ObjectCache.Clear();
            Schema schema = (Schema)new XmlSerializer(typeof(Schema)).Deserialize(reader);

            if (!eventDeserializerProvider.TryGetDeserializer(schema, out IEventDeserializer eventDeserializer))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();

            while (reader.Name == RowName)
            {
                XmlNode rowNode = doc.ReadNode(reader);
                Event e = eventDeserializer.Deserialize(rowNode, xmlContext, schema);

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

        private void ParseInfoSection(XmlReader reader, XmlParsingContext xmlContext)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode infoNode = doc.ReadNode(reader);
            List<string> counterNames = new List<string>();

            // Navigate through the XML structure to find Events and Formulas
            XmlNodeList eventsAndFormulasNodes = infoNode.SelectNodes(".//key[@name='Events and Formulas']/value");

            if (eventsAndFormulasNodes != null)
            {
                foreach (XmlNode valueNode in eventsAndFormulasNodes)
                {
                    if (!string.IsNullOrWhiteSpace(valueNode.InnerText))
                    {
                        counterNames.Add(valueNode.InnerText.Trim());
                    }
                }
            }

            xmlContext.SetCounterNames(counterNames);

            // Extract the recording's wall-clock start time (ISO-8601 with offset) from info/summary/start-date.
            // This anchors WPA's wall-clock for the trace to when it was *recorded*, not when it was *loaded*.
            XmlNode startDateNode = infoNode.SelectSingleNode(".//summary/start-date");
            if (startDateNode != null && !string.IsNullOrWhiteSpace(startDateNode.InnerText))
            {
                if (DateTimeOffset.TryParse(startDateNode.InnerText.Trim(), out DateTimeOffset startDateOffset))
                {
                    xmlContext.SetRecordingStartUtc(startDateOffset.UtcDateTime);
                }
            }
        }
    }
}