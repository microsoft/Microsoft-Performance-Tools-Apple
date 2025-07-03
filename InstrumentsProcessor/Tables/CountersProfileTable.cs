// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.AccessProviders;
using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Backtrace = InstrumentsProcessor.Parsing.DataModels.Backtrace;


namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CountersProfileTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{1BBF6974-CA72-4177-B950-68F282B04BE2}"),
              "Counters Profile",
              "Events from Counters Profile",
              "Counters Profile Events",
              requiredDataCookers: new List<DataCookerPath>
              {
              CountersProfileCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration timeStampColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("39D4952C-B77E-4724-9126-D707A8FA7C5E"), "TimeStamp"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9CCE7BA2-8C45-4CA0-BFDE-C1A55EF94FEA"), "Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration moduleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3F67A997-5A8E-45F2-B05F-349E4DC001B3"), "Module"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration functionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("66EE77C1-94A8-4AA4-88A4-D73D7CFF3CB1"), "Function"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("42A0AD3A-B6B7-4B14-9D61-CE8202E49EBE"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("A9B312FC-7E20-4122-9A94-3E6157A9CBF2"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("FE8513A1-252B-4BBE-8154-F07A39178CFF"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6E1D538D-CAFC-4C12-8D65-7EEF9DB8F703"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7E2C3825-A088-4166-8E5E-FFFCC1CD8560"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration weightColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("E955D14D-C1FC-4741-9419-F9FD5314567B"), "Weight"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("CA3DB955-E194-4790-8EBB-A5B630376189"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration weightViewportPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4B5A0F97-4F03-46B1-9CBC-97C9B92B8C88"), "Weight (in view)"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = "mN",
            });

        private static readonly ColumnConfiguration weightPercentPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0C0FCE58-1904-40A5-B48D-D9CA74ADF163"), "% Weight") { IsPercent = true, ShortDescription = "Weight is expressed as a percentage of total CPU time that is spent over the currently visible time range" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = "N2",
            });

        private static readonly ColumnVariantProperties baseProperties = new ColumnVariantProperties()
        {
            Label = "Stack frames",
            ColumnName = "Stack",
        };

        private static ColumnVariantDescriptor invertedDescriptor = new ColumnVariantDescriptor(
                new Guid("6d73c485-6fcc-441b-88da-6051de689f6b"),
                new ColumnVariantProperties
                {
                    Label = "Invert",
                    ColumnName = $"{baseProperties.ColumnName} (Inverted)",
                });

        private static readonly HashSet<Guid> seenGuids = new HashSet<Guid>();
        private static int lastIndex = 0;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security.Cryptography", "CA5354:SHA1CannotBeUsed", Justification = "Not a security related usage - just generating probabilistically unique id to identify a column from its name.")]
        public static Guid GenerateGuidFromName(string name)
        {
            // The algorithm below is following the guidance of http://www.ietf.org/rfc/rfc4122.txt
            // Create a blob containing a 16 byte number representing the namespace
            // followed by the unicode bytes in the name.  
            var bytes = new byte[name.Length * 2 + 16];
            uint namespace1 = 0x482C2DB2;
            uint namespace2 = 0xC39047c8;
            uint namespace3 = 0x87F81A15;
            uint namespace4 = 0xBFC130FB;
            // Write the bytes most-significant byte first.  
            for (int i = 3; 0 <= i; --i)
            {
                bytes[i] = (byte)namespace1;
                namespace1 >>= 8;
                bytes[i + 4] = (byte)namespace2;
                namespace2 >>= 8;
                bytes[i + 8] = (byte)namespace3;
                namespace3 >>= 8;
                bytes[i + 12] = (byte)namespace4;
                namespace4 >>= 8;
            }
            // Write out  the name, most significant byte first
            for (int i = 0; i < name.Length; i++)
            {
                bytes[2 * i + 16 + 1] = (byte)name[i];
                bytes[2 * i + 16] = (byte)(name[i] >> 8);
            }

            // Compute the Sha1 hash 
            var sha1 = SHA1.Create(); // CodeQL [SM02196] False positive: this hash is not used for any sort of crytopgraphy :)
            byte[] hash = sha1.ComputeHash(bytes);

            // Create a GUID out of the first 16 bytes of the hash (SHA-1 create a 20 byte hash)
            int a = (((((hash[3] << 8) + hash[2]) << 8) + hash[1]) << 8) + hash[0];
            short b = (short)((hash[5] << 8) + hash[4]);
            short c = (short)((hash[7] << 8) + hash[6]);

            c = (short)((c & 0x0FFF) | 0x5000);   // Set high 4 bits of octet 7 to 5, as per RFC 4122
            Guid guid = new Guid(a, b, c, hash[8], hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
            return guid;
        }

        public static Guid GetGuidForName(string name)
        {
            string nameToUse = name;
            while (true)
            {
                Guid result = GenerateGuidFromName(nameToUse);
                if (!seenGuids.Contains(result))
                {
                    seenGuids.Add(result);
                    return result;
                }

                nameToUse = name + lastIndex;
                ++lastIndex;
            }
        }

        //
        // This method, with this exact signature, is required so that the runtime can 
        // build your table once all cookers have processed their data.
        //
        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<CountersProfileEvent> data =
                requiredData.QueryOutput<List<CountersProfileEvent>>(new DataOutputPath(CountersProfileCooker.DataCookerPath, nameof(CountersProfileCooker.CountersProfileEvent)));
            StackAccessProvider stackAccessProvider = new StackAccessProvider();
            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var timeStampProjection = baseProjection.Compose(Projector.TimeStampProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var stackProjection = baseProjection.Compose(Projector.StackProjector);
            var moduleProjection = stackProjection.Compose(Projector.ModuleProjector);
            var functionProjection = stackProjection.Compose(Projector.FunctionProjector);
            var weightProjection = baseProjection.Compose(Projector.WeightProjector);
            var startTimeProjection = Projection.Select(timeStampProjection, weightProjection, new ReduceTimeMinusDelta());
            var viewportClippedStartTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(startTimeProjection);
            var viewportClippedEndTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(timeStampProjection);
            var clippedWeightColumn = Projection.Select(
                viewportClippedEndTimeProjection,
                viewportClippedStartTimeProjection,
                new ReduceTimeSinceLastDiff());
            var weightPercentProjection =
                Projection.VisibleDomainRelativePercent.Create(clippedWeightColumn);

            // Collect all unique counter names from the data
            var counterNames = new HashSet<object>();
            foreach (var eventData in data)
            {
                if (eventData.CounterValueArray?.CounterValues != null)
                {
                    foreach (var key in eventData.CounterValueArray.CounterValues.Keys)
                    {
                        counterNames.Add(key);
                    }
                }
            }

            // Sort counter names for consistent ordering (convert to list and sort)
            var sortedCounterNames = counterNames.ToList();
            sortedCounterNames.Sort((a, b) => string.Compare(a?.ToString(), b?.ToString(), StringComparison.Ordinal));

            // Create dynamic columns for each counter
            var dynamicCounterColumns = new List<ColumnConfiguration>();
            var dynamicCounterProjections = new List<IProjection<int, long>>();

            foreach (var counterName in sortedCounterNames)
            {
                string displayName = counterName?.ToString() ?? "Unknown";
                var counterColumn = new ColumnConfiguration(
                    new ColumnMetadata(GetGuidForName($"Counter_{displayName}"), displayName),
                    new UIHints
                    {
                        IsVisible = true,
                        AggregationMode = AggregationMode.Sum,
                        Width = 100,
                    });

                // Create a projection for this specific counter
                var counterProjection = baseProjection.Compose(
                    new Func<CountersProfileEvent, long>(e => 
                        e.CounterValueArray?.CounterValues.TryGetValue(counterName, out long value) == true ? value : 0));

                dynamicCounterColumns.Add(counterColumn);
                dynamicCounterProjections.Add(counterProjection);
            }

            // Add all standard columns
            tableBuilderWithRowCount.AddColumn(timeStampColumn, timeStampProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(weightColumn, weightProjection);

            // Add dynamic counter columns
            for (int i = 0; i < dynamicCounterColumns.Count; i++)
            {
                tableBuilderWithRowCount.AddColumn(dynamicCounterColumns[i], dynamicCounterProjections[i]);
            }

            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(stackColumn,
                    stackProjection, stackAccessProvider, builder =>
                    {
                        return builder
                            .WithModes(
                                baseProperties,
                                modeBuilder =>
                                {
                                    return modeBuilder.WithHierarchicalToggle(
                                        invertedDescriptor,
                                        stackProjection,
                                        new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccessProvider));
                                });
                    });
            tableBuilderWithRowCount.AddColumn(moduleColumn, moduleProjection);
            tableBuilderWithRowCount.AddColumn(functionColumn, functionProjection);
            tableBuilderWithRowCount.AddColumn(countPreset, Projection.Constant(1));
            tableBuilderWithRowCount.AddColumn(weightViewportPreset, clippedWeightColumn);
            tableBuilderWithRowCount.AddColumn(weightPercentPreset, weightPercentProjection);

            // Build table configuration with dynamic counter columns
            var configColumns = new List<ColumnConfiguration>
            {
                processNameColumn,
                stackColumn,
                TableConfiguration.PivotColumn,
                countPreset
            };

            // Add dynamic counter columns to the configuration
            configColumns.AddRange(dynamicCounterColumns);

            configColumns.AddRange(new[]
            {
                weightViewportPreset,
                timeStampColumn,
                TableConfiguration.GraphColumn,
                weightPercentPreset
            });

            var tableConfig = new TableConfiguration("Utilization by Process, Stack")
            {
                Columns = configColumns.ToArray(),
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, timeStampColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, weightColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }

        private struct ReduceTimeMinusDelta
            : IFunc<int, Timestamp, TimestampDelta, Timestamp>
        {
            public Timestamp Invoke(int value, Timestamp timestamp, TimestampDelta delta)
            {
                return timestamp - delta;
            }
        }

        private struct ReduceTimeSinceLastDiff
            : IFunc<int, Timestamp, Timestamp, TimestampDelta>
        {
            public TimestampDelta Invoke(int value, Timestamp timeSinceLast1, Timestamp timeSinceLast2)
            {
                return timeSinceLast1 - timeSinceLast2;
            }
        }
    }
}