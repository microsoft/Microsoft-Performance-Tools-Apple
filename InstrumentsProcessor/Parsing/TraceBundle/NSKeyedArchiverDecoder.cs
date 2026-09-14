// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Claunia.PropertyList;
using System.Collections.Generic;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Minimal NSKeyedArchiver decoder for reading Apple plist archives
    /// found inside .trace bundles.
    /// </summary>
    internal static class NSKeyedArchiverDecoder
    {
        public static object Resolve(NSObject[] objects, NSObject uid)
        {
            int idx;
            if (uid is UID u) idx = (int)u.ToUInt64();
            else if (uid is NSNumber n) idx = n.ToInt();
            else return uid;

            if (idx == 0) return null;
            return idx < objects.Length ? objects[idx] : null;
        }

        public static Dictionary<string, object> GetDict(NSObject[] objects, NSDictionary dict)
        {
            var result = new Dictionary<string, object>();

            // NSKeyedArchiver encodes NSDictionary as { "NS.keys": [...], "NS.objects": [...] }.
            // Decode the paired arrays into actual key → value entries.
            if (dict.ContainsKey("NS.keys"))
            {
                var nsKeys = dict.ObjectForKey("NS.keys") as NSArray;
                var nsObjs = dict.ObjectForKey("NS.objects") as NSArray;
                if (nsKeys != null && nsObjs != null)
                {
                    for (int i = 0; i < nsKeys.Count && i < nsObjs.Count; i++)
                    {
                        var resolvedKey = Resolve(objects, nsKeys[i])?.ToString();
                        if (resolvedKey != null)
                        {
                            result[resolvedKey] = Resolve(objects, nsObjs[i]);
                        }
                    }
                }
                return result;
            }

            // Fallback: plain dict without NS.keys encoding
            foreach (var key in dict.Keys)
            {
                var val = Resolve(objects, dict.ObjectForKey(key));
                result[key] = val;
            }
            return result;
        }

        public static List<object> GetArray(NSObject[] objects, NSObject obj)
        {
            var result = new List<object>();
            if (obj is NSDictionary d && d.ContainsKey("NS.objects"))
            {
                var items = d.ObjectForKey("NS.objects") as NSArray;
                if (items != null)
                    foreach (var item in items)
                        result.Add(Resolve(objects, item));
            }
            return result;
        }
    }
}