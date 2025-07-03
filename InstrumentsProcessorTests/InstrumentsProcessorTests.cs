// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.Toolkit.Engine;
using System.Reflection;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK;
using System.Linq;

namespace InstrumentsProcessorTests
{
    public class InstrumentsProcessorTests
    {
        private static List<DataCookerPath> DataCookerPaths = new List<DataCookerPath>() 
        {
            InstrumentsProcessor.Cookers.CountersProfileCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.CpuProfileCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.DeviceThermalStateIntervalCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.DisplayVsyncIntervalCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.MetalGpuIntervalCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.PotentialHangCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.SyscallCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.SyscallNameMapCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.ThreadStateCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.TimeProfileCooker.DataCookerPath,
            InstrumentsProcessor.Cookers.VirtualMemoryCooker.DataCookerPath
        };

        private static List<TableDescriptor> TableDescriptors = new List<TableDescriptor>()
        {
            InstrumentsProcessor.Tables.CountersProfileTable.TableDescriptor,
            InstrumentsProcessor.Tables.CPUPreciseTable.TableDescriptor,
            InstrumentsProcessor.Tables.CPUProfiletable.TableDescriptor,
            InstrumentsProcessor.Tables.CpuSamplingTable.TableDescriptor,
            InstrumentsProcessor.Tables.DisplayVsyncIntervalTable.TableDescriptor,
            InstrumentsProcessor.Tables.MemoryTable.TableDescriptor,
            InstrumentsProcessor.Tables.MetalGpuIntervalTable.TableDescriptor,
            InstrumentsProcessor.Tables.PotentialHangTable.TableDescriptor,
            InstrumentsProcessor.Tables.SyscallTable.TableDescriptor,
            InstrumentsProcessor.Tables.ThermalTable.TableDescriptor
        };

        [Theory]
        [InlineData(@"TestData\display_vsync_trace")]
        [InlineData(@"TestData\gpu_trace")]
        [InlineData(@"TestData\system_trace")]
        [InlineData(@"TestData\trace_memory")]
        [InlineData(@"TestData\trace_thermal")]
        [InlineData(@"TestData\cache")]
        public void BaselineTest(string inputFilePathWithoutFileExtension)
        {
            string inputFilePath = inputFilePathWithoutFileExtension + ".xml";
            string baselineFilePath = inputFilePathWithoutFileExtension + "_baseline.xml";
            string outputFilePath = inputFilePathWithoutFileExtension + "_output.xml";

            Assert.True(File.Exists(inputFilePath));
            Assert.True(File.Exists(baselineFilePath));

            string? pluginPath = Path.GetDirectoryName(typeof(InstrumentsProcessor.InstrumentsProcessingSource).Assembly.Location);
            Assert.NotNull(pluginPath);

            using PluginSet plugins = PluginSet.Load(pluginPath);

            using DataSourceSet dataSources = DataSourceSet.Create(plugins);
            dataSources.AddFile(inputFilePath);

            EngineCreateInfo createInfo = new EngineCreateInfo(dataSources.AsReadOnly());
            using Engine engine = Engine.Create(createInfo);
            
            foreach (DataCookerPath dataCookerPath in DataCookerPaths)
            {
                engine.EnableCooker(dataCookerPath);
            }

            foreach (TableDescriptor tableDescriptor in TableDescriptors)
            {
                engine.EnableTable(tableDescriptor);
            }

            RuntimeExecutionResults results = engine.Process();

            StubVisibleDomainRegion visibleDomainRegion = new();
            visibleDomainRegion.Domain = TimeRange.Default;

            using (StreamWriter streamWriter = new StreamWriter(outputFilePath))
            {
                foreach (TableDescriptor tableDescriptor in TableDescriptors)
                {
                    streamWriter.WriteLine(tableDescriptor.Name);
                    ITableResult tableResult = results.BuildTable(tableDescriptor);

                    foreach (IDataColumn column in tableResult.Columns)
                    {
                        Type dataColumnType = column.GetType();
                        PropertyInfo? projectorProperty = dataColumnType.GetProperty("Projector");
                        IProjectionDescription? projectorDescriptor = (IProjectionDescription?)projectorProperty?.GetValue(column);
                        VisibleDomainSensitiveProjection.NotifyVisibleDomainChanged(projectorDescriptor, visibleDomainRegion);
                    }

                    for (int row = 0; row < tableResult.RowCount; row++)
                    {
                        List<object> rowValues = new List<object>();

                        foreach (IDataColumn column in tableResult.Columns)
                        {
                            object value = column.Project(row);

                            rowValues.Add(value);
                        }

                        streamWriter.WriteLine(string.Join(",", rowValues));
                    }
                }
            }

            using (var baselineReader = new StreamReader(baselineFilePath))
            using (var outputReader = new StreamReader(outputFilePath))
            {
                string? baselineLine = null;
                string? outputLine = null;

                while ((baselineLine = baselineReader.ReadLine()) != null && (outputLine = outputReader.ReadLine()) != null)
                {
                    Assert.True(baselineLine == outputLine);
                }

                Assert.True(baselineReader.EndOfStream && outputReader.EndOfStream);
            }
        }

        private class StubVisibleDomainRegion
            : IVisibleDomainRegion
        {
            public TimeRange Domain { get; set; }

            public TAggregate? AggregateVisibleRows<T, TAggregate>(
                IProjection<int, T> projection,
                AggregationMode aggregationMode)
            {
                return default;
            }
        }
    }
}
