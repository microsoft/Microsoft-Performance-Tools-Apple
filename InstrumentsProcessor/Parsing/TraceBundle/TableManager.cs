// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Claunia.PropertyList;
using System.Collections.Generic;
using System.IO;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal record StoreInfo(string SchemaName, string StorePath, int Side);

    /// <summary>
    /// Parses the table-manager/tables.plist to discover all stores
    /// (schema name, path, side) inside a .trace bundle run.
    /// </summary>
    internal static class TableManager
    {
        public static List<StoreInfo> ParseTablesPlist(string plistPath, string baseDir)
        {
            var data = Decompressor.ReadCompressed(plistPath);
            NSDictionary plist;
            try { plist = (NSDictionary)PropertyListParser.Parse(data); }
            catch { return new List<StoreInfo>(); }

#pragma warning disable CS0612
            var objects = ((NSArray)plist.ObjectForKey("$objects")).GetArray();
#pragma warning restore CS0612
            var top = (NSDictionary)plist.ObjectForKey("$top");
            var root = NSKeyedArchiverDecoder.Resolve(objects, top.ObjectForKey("root")) as NSDictionary;
            if (root == null) return new List<StoreInfo>();

            // Find tableDetails
            NSDictionary tableDetailsDict = null;
            if (root.ContainsKey("NS.keys"))
            {
                var keys = root.ObjectForKey("NS.keys") as NSArray;
                var vals = root.ObjectForKey("NS.objects") as NSArray;
                if (keys != null && vals != null)
                {
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (NSKeyedArchiverDecoder.Resolve(objects, keys[i])?.ToString() == "tableDetails")
                        {
                            tableDetailsDict = NSKeyedArchiverDecoder.Resolve(objects, vals[i]) as NSDictionary;
                            break;
                        }
                    }
                }
            }
            if (tableDetailsDict == null) return new List<StoreInfo>();

            var results = new List<StoreInfo>();
            foreach (var k in tableDetailsDict.Keys)
            {
                if (!k.StartsWith('$') || !int.TryParse(k.Substring(1), out int idx) || idx == 0)
                    continue;

                var entryObj = NSKeyedArchiverDecoder.Resolve(objects, tableDetailsDict.ObjectForKey(k)) as NSDictionary;
                if (entryObj == null || !entryObj.ContainsKey("NS.keys")) continue;

                var ed = NSKeyedArchiverDecoder.GetDict(objects, entryObj);
                string storeSubpath = ed.TryGetValue("storeSubpath", out var sp) ? sp?.ToString() ?? "" : "";
                int side = ed.TryGetValue("side", out var sv) && sv is NSNumber sn ? sn.ToInt() : 0;

                string schemaName = "";
                if (ed.TryGetValue("spec", out var specObj))
                {
                    if (specObj is NSDictionary specDict)
                    {
                        Dictionary<string, object> spec;
                        if (specDict.ContainsKey("NS.keys"))
                            spec = NSKeyedArchiverDecoder.GetDict(objects, specDict);
                        else
                        {
                            spec = new Dictionary<string, object>();
                            foreach (var sk in specDict.Keys)
                                spec[sk] = specDict.ObjectForKey(sk);
                        }
                        if (spec.TryGetValue("schemaName", out var sn2))
                        {
                            if (sn2 is UID uid2)
                                schemaName = objects[(int)uid2.ToUInt64()]?.ToString() ?? "";
                            else
                                schemaName = sn2?.ToString() ?? "";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(storeSubpath))
                {
                    var fullPath = Path.Combine(baseDir, storeSubpath);
                    results.Add(new StoreInfo(schemaName, fullPath, side));
                }
            }
            return results;
        }
    }
}