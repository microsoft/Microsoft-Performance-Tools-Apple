// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace InstrumentsProcessor.Parsing
{
    public class ObjectCache
    {
        private Dictionary<int, object> cache = new Dictionary<int, object>();

        public ObjectCache()
        {
            cache = new Dictionary<int, object>();
        }

        public void CacheObject(int objId, object obj)
        {
            if (cache.ContainsKey(objId))
            {
                throw new ArgumentException();
            }

            cache[objId] = obj;
        }

        public void CacheObject(XmlNode node, object obj)
        {
            XmlNode idAttribute = node.Attributes.GetNamedItem("id");

            if (idAttribute != null)
            {
                int objId = int.Parse(idAttribute.InnerText);

                CacheObject(objId, obj);
            }
        }

        public void CacheObject(XElement root, object obj)
        {
            if (root != null)
            {
                int objId = int.Parse(root.Attribute("id")?.Value);

                CacheObject(objId, obj);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public bool TryLookupObject(int objId, out object obj)
        {
            return cache.TryGetValue(objId, out obj);
        }

        public bool TryLookupObject(XmlNode node, out object obj)
        {
            XmlNode refAttribute = node.Attributes.GetNamedItem("ref");

            if (refAttribute != null)
            {
                int objid = int.Parse(refAttribute.InnerText);

                return TryLookupObject(objid, out obj);
            }
            else
            {
                obj = null;

                return false;
            }
        }

        public bool TryGetRefId(XmlNode node, out int refId)
        {
            XmlNode refAttribute = node.Attributes.GetNamedItem("ref");
            refId = refAttribute != null ? int.Parse(refAttribute.InnerText) : 0;

            return refAttribute != null;
        }

        public bool TryLookupObject(XElement root, out object obj)
        {
            
            if (root != null)
            {
                int objId = int.Parse(root.Attribute("id")?.Value);

                return TryLookupObject(objId, out obj);
            }
            else
            {
                obj = null;

                return false;
            }
        }

        public void Clear()
        {
            cache.Clear();
        }
    }
}