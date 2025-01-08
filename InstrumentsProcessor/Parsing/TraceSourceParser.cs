// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using System.Collections.Generic;
using System;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using InstrumentsProcessor.Parsing.Events;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Performance.SDK;

namespace InstrumentsProcessor.Parsing
{
    public sealed class TraceSourceParser
        : SourceParser<Event, ParsingContext, Type>
    {
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
            int totalRows = 0;

            foreach (IDataSource dataSource in this.dataSources)
            {
                totalRows += GetRowCount(dataSource);
            }

            Timestamp? firstEventTimestamp = null;
            Timestamp? lastEventTimestamp = null;
            int rowCount = 0;

            foreach (IDataSource dataSource in this.dataSources)
            {
                foreach (XmlNode node in GetNodesToProcess(dataSource))
                {
                    ObjectCache cache = new ObjectCache();
                    Schema schema;

                    using (StringReader reader = new StringReader(node.FirstChild.OuterXml))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(Schema));
                        schema = (Schema)serializer.Deserialize(reader);
                    }

                    if (!eventDeserializerProvider.TryGetDeserializer(schema, out IEventDeserializer eventDeserializer))
                    {
                        continue;
                    }

                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        if (childNode.Name != "row")
                        {
                            continue;
                        }

                        Event e = eventDeserializer.Deserialize(childNode, cache, schema);

                        dataProcessor.ProcessDataElement(e, this.context, cancellationToken);

                        if (firstEventTimestamp == null || firstEventTimestamp.Value > e.Timestamp)
                        {
                            firstEventTimestamp = e.Timestamp;
                        }

                        if (lastEventTimestamp == null || lastEventTimestamp.Value < e.Timestamp)
                        {
                            lastEventTimestamp = e.Timestamp;
                        }

                        rowCount++;
                        progress.Report((int)(rowCount / (double)totalRows * 100));
                    }
                }
            }

            long firstEventTimestampNanoseconds = firstEventTimestamp.HasValue ? firstEventTimestamp.Value.ToNanoseconds : 0;
            long lastEventTimestampnanoseconds = lastEventTimestamp.HasValue ? lastEventTimestamp.Value.ToNanoseconds : firstEventTimestampNanoseconds + 1;
            DateTime firstEventWallClockUtc = DateTime.UtcNow;
            dataSourceInfo = new DataSourceInfo(firstEventTimestampNanoseconds, lastEventTimestampnanoseconds, firstEventWallClockUtc);
        }

        private static IEnumerable<XmlNode> GetNodesToProcess(IDataSource dataSource)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                yield break;
            }

            XmlDocument doc = GetXmlDocument(fileDataSource);

            if (doc.ChildNodes.Count != 2 ||
                doc.ChildNodes[1].Name != "root")
            {
                yield break;
            }

            XmlNode root = doc.ChildNodes[1];

            foreach (XmlNode traceQueryResult in root.ChildNodes)
            {
                if (traceQueryResult.Name == "trace-query-result" &&
                    traceQueryResult.ChildNodes.Count == 1 &&
                    traceQueryResult.ChildNodes[0].Name == "node")
                {
                    yield return traceQueryResult.ChildNodes[0];
                }
            }
        }

        private static int GetRowCount(IDataSource dataSource)
        {
            int rowCount = 0;

            foreach (XmlNode node in GetNodesToProcess(dataSource))
            {
                rowCount += node.ChildNodes.Count - 1;
            }

            return rowCount;
        }

        private static XmlDocument GetXmlDocument(FileDataSource fileDataSource)
        {
            string xmlContent = File.ReadAllText(fileDataSource.FullPath);

            // Remove all <?xml version="1.0"?> declarations except the first one
            string cleanedXmlContent = Regex.Replace(xmlContent, @"<\?xml version=""1.0""\?>", "", RegexOptions.Multiline);
            cleanedXmlContent = "<?xml version=\"1.0\"?><root>" + cleanedXmlContent + "</root>";

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(cleanedXmlContent);

            return doc;
        }
    }
}