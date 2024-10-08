// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor
{
    [ProcessingSource(
       "{2aedc2bb-5422-469e-9a85-eec88cda5df8}",  // The GUID must be unique for your ProcessingSource. You can use 
                                                  // Visual Studio's Tools -> Create Guid… tool to create a new GUID
       "iOS Trace Processor",                      // The ProcessingSource MUST have a name
       "Process trace captured on Apple device")]          // The ProcessingSource MUST have a description
    [FileDataSource(
       ".xml",                                 // A file extension is REQUIRED
       "Xml files")]                             // A description is OPTIONAL. The description is what appears in the 
                                                 // file open menu to help users understand what the file type actually is
    public class MyProcessingSource : ProcessingSource
    {
        public MyProcessingSource() : base()
        {
        }

        protected override bool IsDataSourceSupportedCore(IDataSource dataSource)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return false;
            }

            return true;
        }

        protected override ICustomDataProcessor CreateProcessorCore(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options)
        {
            //
            // Create a new instance of a class implementing ICustomDataProcessor here to process the specified data 
            // sources.
            // Note that you can have more advanced logic here to create different processors if you would like based 
            // on the file, or any other criteria.
            // You are not restricted to always returning the same type from this method.
            //

            var parser = new TraceSourceParser(dataSources);

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
                    Name = "Author Name",
                    Address = "Author Email",
                    EmailAddresses = new[]
                    {
                        "owners@mycompany.com",
                    },
                },
            },
                LicenseInfo = null,
                ProjectInfo = null,
                CopyrightNotice = $"Copyright (C) {DateTime.Now.Year}",
                AdditionalInformation = null,
            };
        }
    }
}