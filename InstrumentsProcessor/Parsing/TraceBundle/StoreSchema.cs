// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Claunia.PropertyList;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal enum FieldType
    {
        U32 = 1,
        U64 = 2,
        TopoInterval = 6,
        TopoPoint = 7
    }

    internal class FieldDescriptor
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public FieldType Type { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
    }

    internal class ColumnInfo
    {
        public string Mnemonic { get; set; } = "";
        public string Name { get; set; } = "";
        public string EngineeringType { get; set; } = "";
        public string TopoField { get; set; } = "";
    }

    /// <summary>
    /// Parses a bulkstore's descriptor and schema.xml to determine
    /// the binary row layout and column metadata.
    /// </summary>
    internal class StoreSchema
    {
        private static readonly Dictionary<FieldType, int> TopoSizes = new Dictionary<FieldType, int>
        {
            [FieldType.TopoInterval] = 24,
            [FieldType.TopoPoint] = 16
        };
        private static readonly Dictionary<FieldType, int> FieldSizes = new Dictionary<FieldType, int>
        {
            [FieldType.U32] = 4,
            [FieldType.U64] = 8
        };

        public string SchemaName { get; }
        public string StorePath { get; }
        public List<FieldDescriptor> Fields { get; } = new List<FieldDescriptor>();
        public int RowSize { get; private set; }
        public FieldType? TopoType { get; private set; }
        public int TopoSize { get; private set; }
        public List<ColumnInfo> Columns { get; } = new List<ColumnInfo>();

        public StoreSchema(string storePath, string schemaName)
        {
            StorePath = storePath;
            SchemaName = schemaName;
            ParseDescriptor();
            ParseSchemaXml();
        }

        private void ParseDescriptor()
        {
            var descPath = Path.Combine(StorePath, "bulkstore_descriptor");
            if (!File.Exists(descPath)) return;

            var data = Decompressor.ReadCompressed(descPath);
            NSDictionary plist;
            try { plist = (NSDictionary)PropertyListParser.Parse(data); }
            catch { return; }

#pragma warning disable CS0612
            var objects = ((NSArray)plist.ObjectForKey("$objects")).GetArray();
#pragma warning restore CS0612
            var top = (NSDictionary)plist.ObjectForKey("$top");
            var rootObj = NSKeyedArchiverDecoder.Resolve(objects, top.ObjectForKey("root"));

            NSDictionary eventDict = null;
            if (rootObj is NSDictionary rd && rd.ContainsKey("$0"))
                eventDict = NSKeyedArchiverDecoder.Resolve(objects, rd.ObjectForKey("$0")) as NSDictionary;
            else
                eventDict = rootObj as NSDictionary;

            if (eventDict == null || !eventDict.ContainsKey("NS.keys")) return;

            var ed = NSKeyedArchiverDecoder.GetDict(objects, eventDict);
            if (ed.TryGetValue("_maxEventSize", out var mes) && mes is NSNumber mesn)
                RowSize = mesn.ToInt();

            // Extract fields
            var nsKeys = eventDict.ObjectForKey("NS.keys") as NSArray;
            var nsObjs = eventDict.ObjectForKey("NS.objects") as NSArray;
            if (nsKeys == null || nsObjs == null) return;

            for (int i = 0; i < nsKeys.Count; i++)
            {
                var kname = NSKeyedArchiverDecoder.Resolve(objects, nsKeys[i])?.ToString();
                if (kname != "_fields") continue;

                var fieldsArrObj = NSKeyedArchiverDecoder.Resolve(objects, nsObjs[i]) as NSDictionary;
                if (fieldsArrObj == null) break;

                var fieldUids = fieldsArrObj.ObjectForKey("NS.objects") as NSArray;
                if (fieldUids == null) break;

                foreach (var fu in fieldUids)
                {
                    var fobj = NSKeyedArchiverDecoder.Resolve(objects, fu) as NSDictionary;
                    if (fobj == null) continue;
                    NSDictionary inner = null;
                    if (fobj.ContainsKey("$0"))
                        inner = NSKeyedArchiverDecoder.Resolve(objects, fobj.ObjectForKey("$0")) as NSDictionary;
                    if (inner == null) continue;

                    var fd = NSKeyedArchiverDecoder.GetDict(objects, inner);
                    string name = fd.TryGetValue("_name", out var nv) ? nv?.ToString() ?? "" : "";
                    int index = fd.TryGetValue("_index", out var iv) && iv is NSNumber ivn ? ivn.ToInt() : 0;
                    int ftype = fd.TryGetValue("_type", out var tv) && tv is NSNumber tvn ? tvn.ToInt() : 0;
                    Fields.Add(new FieldDescriptor { Name = name, Index = index, Type = (FieldType)ftype });
                }
                break;
            }

            // Compute offsets — topology field first, then data fields by index
            FieldDescriptor topoField = null;
            var dataFields = new List<FieldDescriptor>();
            foreach (var f in Fields)
            {
                if (TopoSizes.ContainsKey(f.Type))
                    topoField = f;
                else
                    dataFields.Add(f);
            }

            if (topoField != null)
            {
                TopoType = topoField.Type;
                TopoSize = TopoSizes[topoField.Type];
                topoField.Offset = 0;
                topoField.Size = TopoSize;
            }

            dataFields.Sort((a, b) => a.Index.CompareTo(b.Index));
            int offset = TopoSize;
            foreach (var f in dataFields)
            {
                f.Size = FieldSizes.ContainsKey(f.Type) ? FieldSizes[f.Type] : 4;
                f.Offset = offset;
                offset += f.Size;
            }
        }

        private void ParseSchemaXml()
        {
            var schemaPath = Path.Combine(StorePath, "schema.xml");
            if (!File.Exists(schemaPath)) return;
            var data = Decompressor.ReadCompressed(schemaPath);
            try
            {
                var doc = XDocument.Parse(Encoding.UTF8.GetString(data));
                foreach (var col in doc.Descendants("column"))
                {
                    Columns.Add(new ColumnInfo
                    {
                        Mnemonic = col.Attribute("mnemonic")?.Value ?? "",
                        Name = col.Attribute("engineeringName")?.Value ?? "",
                        EngineeringType = col.Attribute("engineeringType")?.Value ?? "",
                        TopoField = col.Attribute("topologyField")?.Value ?? ""
                    });
                }
            }
            catch { }
        }

        public bool IsInterval => TopoType == FieldType.TopoInterval;

        public List<FieldDescriptor> GetDataFields() =>
            Fields.Where(f => !TopoSizes.ContainsKey(f.Type))
                  .OrderBy(f => f.Offset).ToList();
    }
}