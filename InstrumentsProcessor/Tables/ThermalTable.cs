// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class ThermalTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{ab2184b6-ca4b-459b-8c41-43e4db4a4952}"),
              "Thermal",
              "Events from Device Thermal State Intervals",
              "Device Thermal State Intervals",
              requiredDataCookers: new List<DataCookerPath>
              {
              DeviceThermalStateIntervalCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration switchInTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5373d778-7716-4455-80a3-88e65bc65bdb"), "Switch -In Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration switchOutTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("453b2afe-a409-44c8-826e-5073686361b2"), "Switch-Out Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped
            });

        private static readonly ColumnConfiguration thermalStateColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1ef77bbd-9473-433d-ae59-8817cef08aa9"), "State"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });
        //
        // This method, with this exact signature, is required so that the runtime can 
        // build your table once all cookers have processed their data.
        //
        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<DeviceThermalStateIntervalEvent> data =
                requiredData.QueryOutput<List<DeviceThermalStateIntervalEvent>>(new DataOutputPath(DeviceThermalStateIntervalCooker.DataCookerPath,
                nameof(DeviceThermalStateIntervalCooker.DeviceThermalStateIntervalEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);
            var switchInTimeProjection = baseProjection.Compose(Projector.SwitchInTimeProjector);
            var switchOutTimeProjection = baseProjection.Compose(Projector.SwitchOutTimeProjector);
            var thermalStateProjection = baseProjection.Compose(Projector.ThermalStateProjector);

            tableBuilderWithRowCount.AddColumn(switchInTimeColumn, switchInTimeProjection);
            tableBuilderWithRowCount.AddColumn(switchOutTimeColumn, switchOutTimeProjection);
            tableBuilderWithRowCount.AddColumn(thermalStateColumn, thermalStateProjection);

            var tableConfig = new TableConfiguration("State")
            {
                Columns = new[]
                {
                    thermalStateColumn,
                    TableConfiguration.PivotColumn,
                    TableConfiguration.GraphColumn,
                    switchInTimeColumn,
                    switchOutTimeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, switchInTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, switchOutTimeColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}