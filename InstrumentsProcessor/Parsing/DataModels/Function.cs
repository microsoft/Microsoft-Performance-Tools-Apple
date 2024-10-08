// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Function
    {
        public string Name { get; private set; }

        public string Address { get; private set; }
        
        public Function(string name, string address) 
        {
            Name = name;
            Address = address;
        }
    }
}
