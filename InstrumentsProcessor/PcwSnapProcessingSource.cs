// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor
{
    [ProcessingSource(
       "{a7f3c8d1-6e4b-4a92-b1d5-3f8e0c2a9b4d}",
       "PcwSnap Trace Processor",
       "Process pcwsnap snapshot traces captured on Apple devices")]
    [FileDataSource(
       ".pcwsnap",
       "PcwSnap snapshot trace files")]
    public class PcwSnapProcessingSource : ProcessingSource
    {
        public PcwSnapProcessingSource() : base()
        {
        }

        protected override bool IsDataSourceSupportedCore(IDataSource dataSource)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return false;
            }

            return fileDataSource.FullPath.EndsWith(".pcwsnap", StringComparison.OrdinalIgnoreCase);
        }

        protected override ICustomDataProcessor CreateProcessorCore(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options)
        {
            var parser = new PcwSnapSourceParser(dataSources);

            return new TraceProcessor(
                parser,
                options,
                this.ApplicationEnvironment,
                processorEnvironment);
        }

        public override ProcessingSourceInfo GetAboutInfo()
        {
            return new ProcessingSourceInfo
            {
                Owners = new[]
                {
                    new ContactInfo
                    {
                        Name = "Author Name: Hani Nemati, Benjamin Galindo-Navarro",
                        Address = "Author Email",
                        EmailAddresses = new[]
                        {
                            "hanemati@microsoft.com",
                            "benjaming@microsoft.com"
                        },
                    },
                },
                LicenseInfo = new LicenseInfo()
                {
                    Name = "MIT",
                    Uri = "https://mit-license.org/",
                    Text = "The MIT License (MIT)"
                },
                ProjectInfo = new ProjectInfo()
                {
                    Uri = "https://github.com/microsoft/Microsoft-Performance-Tools-Apple"
                },
                CopyrightNotice = $"Copyright (C) {DateTime.Now.Year}",
                AdditionalInformation = null,
            };
        }
    }
}
