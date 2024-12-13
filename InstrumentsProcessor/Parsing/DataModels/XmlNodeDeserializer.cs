// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    /// <summary>
    /// The XmlNodeDeserializer class is responsible for deserializing XML nodes into objects of type T.
    /// It supports deserialization of properties with and without custom deserialization logic.
    /// The class checks the ObjectCache for existing instances before creating new ones.
    /// It recursively creates deserializers for each property of the class being deserialized.
    /// Objects can override the default deserialization behavior by implementing the IPropertyDeserializer interface
    /// and marking properties with the CustomDeserialization attribute.
    /// </summary>
    public class XmlNodeDeserializer<T> where T : new()
    {
        private readonly Dictionary<string, object> _propertyDeserializers;

        public XmlNodeDeserializer()
        {
            _propertyDeserializers = new Dictionary<string, object>();
        }

        public T Deserialize(XmlNode node, ObjectCache cache)
        {
            if (node == null || node.Name == "sentinel")
            {
                return default;
            }

            if (cache.TryGetRefId(node, out int refId))
            {
                if (!cache.TryLookupObject(node, out object cachedObject))
                {
                    // We did not properly cache the object earlier
                    // This can happen if an event does not parse all of the XML in a schema
                    // To avoid trying to parse ref nodes, we just return null here
                    Debug.Assert(false);
                    return default;
                }

                return (T)cachedObject;
            }

            // Create an instance of T
            T instance = new T();

            // Loop over the properties of T and the child nodes
            List<PropertyInfo> propertiesWithDefaultDeserialization = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<CustomDeserializationAttribute>() == null)
                .ToList();

            // First, deserialize properties with the custom attribute
            if (instance is IPropertyDeserializer propertyDeserializer)
            {
                List<PropertyInfo> propertiesWithCustomDeserialization = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<CustomDeserializationAttribute>() != null)
                .ToList();

                foreach (PropertyInfo property in propertiesWithCustomDeserialization)
                {
                    object propertyValue = propertyDeserializer.DeserializeProperty(node, cache, property);
                    property.SetValue(instance, propertyValue);
                }
            }
            
            // Ensure the number of properties with default deserialization is less than or equal to the number of child nodes
            if (propertiesWithDefaultDeserialization.Count > node.ChildNodes.Count)
            {
                // Sometimes, frames or threads are empty this is okay. Should returning the current object be the default behavior or should we throw an exception in this case?
                // TODO: Maybe add an interface that objects can implement to specify behavior when there are not enough child nodes
                if (typeof(T) == typeof(Frame) || typeof(T) == typeof(Thread))
                {
                    cache.CacheObject(node, default);

                    return default;
                }

                throw new InvalidOperationException($"The number of properties in type {typeof(T).FullName} ({propertiesWithDefaultDeserialization.Count}) with default serialization is greater than number of child nodes in the XML ({node.ChildNodes.Count}).");
            }

            // Then, deserialize properties without the custom attribute
            for (int i = 0; i < propertiesWithDefaultDeserialization.Count; i++)
            {
                PropertyInfo property = propertiesWithDefaultDeserialization[i];
                object propertyValue = DeserializeProperty(node.ChildNodes[i], property, cache);
                property.SetValue(instance, propertyValue);
            }

            cache.CacheObject(node, instance);

            return instance;
        }

        private object DeserializeProperty(XmlNode node, PropertyInfo property, ObjectCache cache)
        {
            if (!_propertyDeserializers.TryGetValue(property.Name, out object deserializer))
            {
                Type propertyType = property.PropertyType;

                // Check for parameterless constructor
                if (propertyType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"Property {property.Name} of type {propertyType.FullName} must have a public parameterless constructor.");
                }

                Type deserializerType = typeof(XmlNodeDeserializer<>).MakeGenericType(propertyType);
                deserializer = Activator.CreateInstance(deserializerType);
                _propertyDeserializers[property.Name] = deserializer;
            }

            MethodInfo deserializeMethod = deserializer.GetType().GetMethod("Deserialize");
            object propertyValue = deserializeMethod.Invoke(deserializer, new object[] { node, cache });

            return propertyValue;
        }
    }
}
