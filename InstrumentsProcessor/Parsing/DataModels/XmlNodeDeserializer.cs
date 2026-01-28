// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Performance.SDK;

namespace InstrumentsProcessor.Parsing.DataModels
{
    using PropertyDeserializer = Action<object, XmlNode, XmlParsingContext>;

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
        private static readonly List<PropertyDeserializer> defaultDeserializers = CollectDefaultDeserializers();
        private static readonly List<PropertyDeserializer> customDeserializers = CollectCustomDeserializers();

        private static List<PropertyDeserializer> CollectDefaultDeserializers()
        {
            var deserializers = new List<PropertyDeserializer>();

            List<PropertyInfo> properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<CustomDeserializationAttribute>() == null)
                .ToList();

            foreach (PropertyInfo property in properties)
            {
                Type propertyType = property.PropertyType;

                // Check for parameterless constructor
                if (propertyType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"Property {property.Name} of type {propertyType.FullName} must have a public parameterless constructor.");
                }

                Type deserializerType = typeof(XmlNodeDeserializer<>).MakeGenericType(propertyType);
                object deserializer = Activator.CreateInstance(deserializerType);

                MethodInfo deserializeMethod = deserializer.GetType().GetMethod("Deserialize");
                deserializers.Add((instance, node, context) =>
                {
                    object propertyValue = deserializeMethod.Invoke(deserializer, new object[] { node, context });
                    property.SetValue(instance, propertyValue);
                });
            }

            return deserializers;
        }

        private static List<PropertyDeserializer> CollectCustomDeserializers()
        {
            var deserializers = new List<PropertyDeserializer>();

            if (typeof(T).Implements<IPropertyDeserializer>())
            { 
                List<PropertyInfo> properties = typeof(T).GetProperties()
                    .Where(p => p.GetCustomAttribute<CustomDeserializationAttribute>() != null)
                    .ToList();

                foreach (PropertyInfo property in properties)
                {
                    deserializers.Add((instance, node, context) =>
                    {
                        object propertyValue = ((IPropertyDeserializer)instance).DeserializeProperty(node, context, property);
                        property.SetValue(instance, propertyValue);
                    });
                }
            }

            return deserializers;
        }

        public T Deserialize(XmlNode node, XmlParsingContext context)
        {
            if (node == null || node.Name == "sentinel")
            {
                return default;
            }

            if (context.ObjectCache.TryGetRefId(node, out int refId))
            {
                if (!context.ObjectCache.TryLookupObject(node, out object cachedObject))
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

            // First, deserialize properties with the custom attribute
            foreach (var deserialize in customDeserializers)
            {
                deserialize(instance, node, context);
            }
            
            // Ensure the number of properties with default deserialization is less than or equal to the number of child nodes
            if (defaultDeserializers.Count > node.ChildNodes.Count)
            {
                // Sometimes, frames or threads are empty this is okay. Should returning the current object be the default behavior or should we throw an exception in this case?
                // TODO: Maybe add an interface that objects can implement to specify behavior when there are not enough child nodes
                if (typeof(T) == typeof(Frame) || typeof(T) == typeof(Thread))
                {
                    context.ObjectCache.CacheObject(node, default);

                    return default;
                }

                throw new InvalidOperationException($"The number of properties in type {typeof(T).FullName} ({defaultDeserializers.Count}) with default serialization is greater than number of child nodes in the XML ({node.ChildNodes.Count}).");
            }

            // Then, deserialize properties without the custom attribute
            for (int i = 0; i < defaultDeserializers.Count; i++)
            {
                defaultDeserializers[i](instance, node.ChildNodes[i], context);
            }

            context.ObjectCache.CacheObject(node, instance);

            return instance;
        }
    }
}
