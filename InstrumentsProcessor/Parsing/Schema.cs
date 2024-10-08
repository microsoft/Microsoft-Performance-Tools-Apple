// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace InstrumentsProcessor.Parsing
{
    [XmlRoot("schema")]
    public class Schema
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("col")]
        public List<Column> Columns { get; set; }

        public class Column
        {
            [XmlElement("mneumonic")]
            public string Mneumonic { get; set; }

            [XmlElement("name")]
            public string Name { get; set; }

            [XmlElement("engineering-type")]
            public string EngineeringType { get; set; }
        }
    }
}
