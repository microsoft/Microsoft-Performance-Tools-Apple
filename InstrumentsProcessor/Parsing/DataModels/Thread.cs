// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Thread
    {
        public Integer ThreadId { get; private set; }
        public Process Process { get; private set; }
    }
}
