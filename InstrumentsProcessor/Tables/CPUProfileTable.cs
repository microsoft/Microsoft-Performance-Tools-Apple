// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CPUProfiletable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{7343be40-34d1-46e1-b723-a5db3b4d338f}"),
              "CPU Profile",
              "Events from CPU Profile",
              "CPU Profile Events",
              requiredDataCookers: new List<DataCookerPath>
              {
              CpuProfileCooker.DataCookerPath
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
            List<CpuProfileEvent> data =
                requiredData.QueryOutput<List<CpuProfileEvent>>(new DataOutputPath(CpuProfileCooker.DataCookerPath, nameof(CpuProfileCooker.CpuProfileEvents)));

        }
    }
}