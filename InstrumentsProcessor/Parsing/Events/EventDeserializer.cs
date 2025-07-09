// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// The EventDeserializer class builds deserializers for events, which represent rows of data in a schema.
    /// It uses the ColumnAttribute to identify properties in the event and creates deserializers for each using XmlNodeDeserializer.
    /// When deserializing, it ensures the event properties match the schema's column names and engineering types.
    /// If they match, it deserializes the XML node's child nodes in order, assigning them to the event's properties.
    /// </summary>
    public class EventDeserializer<TEvent> : IEventDeserializer where TEvent : Event, new()
    {
        private readonly Dictionary<(string Name, string EngineeringType), PropertyInfo> propertiesByColumn;
        private readonly Dictionary<string, object> propertyDeserializersByName;
        private readonly Dictionary<string, object> propertyDeserializersByEngineeringType;

        public EventDeserializer()
        {
            propertiesByColumn = new Dictionary<(string Name, string EngineeringType), PropertyInfo>();
            propertyDeserializersByName = new Dictionary<string, object>();
            propertyDeserializersByEngineeringType = new Dictionary<string, object>();

            PropertyInfo[] properties = typeof(TEvent).GetProperties();

            foreach (PropertyInfo property in properties)
            {
                ColumnAttribute attribute = property.GetCustomAttribute<ColumnAttribute>();

                if (attribute != null)
                {
                    propertiesByColumn[(attribute.Name, attribute.EngineeringType)] = property;
                }
            }
        }

        public bool CanDeserialize(Schema schema)
        {
            foreach (Schema.Column column in schema.Columns)
            {
                if (!propertiesByColumn.ContainsKey((column.Name, column.EngineeringType)))
                {
                    return false;
                }
            }

            return true;
        }

        public Event Deserialize(XmlNode node, XmlParsingContext context, Schema schema)
        {
            if (node.ChildNodes.Count != schema.Columns.Count)
            {
                throw new InvalidOperationException($"The number of child nodes in the XML ({node.ChildNodes.Count}) does not match the number of columns in the schema ({schema.Columns.Count}).");
            }

            TEvent instance = new TEvent();

            // Create all property deserializers before we begin deserialization
            CreatePropertyDeserializers(schema);

            for (int i = 0; i < schema.Columns.Count; i++)
            {
                Schema.Column column = schema.Columns[i];

                if (!propertiesByColumn.TryGetValue((column.Name, column.EngineeringType), out PropertyInfo property))
                {
                    throw new InvalidOperationException($"No matching property found for column {column.Name} with engineering type {column.EngineeringType}.");
                }

                if (!propertyDeserializersByName.TryGetValue(property.Name, out object deserializer))
                {
                    throw new InvalidOperationException($"No deserializer found for property {property.Name}.");
                }

                XmlNode childNode = node.ChildNodes[i];
                MethodInfo deserializeMethod = deserializer.GetType().GetMethod("Deserialize");
                object propertyValue = deserializeMethod.Invoke(deserializer, new object[] { childNode, context });
                property.SetValue(instance, propertyValue);

                if (childNode.Name == "narrative" || childNode.Name == "formatted-label")
                {
                    // Try to deserialize top level nodes in dynamic types so we don't miss object definitions
                    ProcessDynamicType(childNode, context);
                }
            }

            return instance;
        }


        private void CreatePropertyDeserializers(Schema schema)
        {
            foreach (var column in schema.Columns)
            {
                if (!propertiesByColumn.TryGetValue((column.Name, column.EngineeringType), out PropertyInfo property))
                {
                    throw new InvalidOperationException($"No matching property found for column {column.Name} with engineering type {column.EngineeringType}.");
                }

                if (!propertyDeserializersByName.ContainsKey(property.Name))
                {
                    Type propertyType = property.PropertyType;

                    if (propertyType.GetConstructor(Type.EmptyTypes) == null)
                    {
                        throw new InvalidOperationException($"Property {property.Name} of type {propertyType.FullName} must have a public parameterless constructor.");
                    }

                    Type deserializerType = typeof(XmlNodeDeserializer<>).MakeGenericType(propertyType);
                    object deserializer = Activator.CreateInstance(deserializerType);
                    propertyDeserializersByName[property.Name] = deserializer;
                    propertyDeserializersByEngineeringType[column.EngineeringType] = deserializer;
                }
            }
        }

        private void ProcessDynamicType(XmlNode dynamicNode, XmlParsingContext context)
        {
            foreach (XmlNode childNode in dynamicNode)
            {
                if (propertyDeserializersByEngineeringType.TryGetValue(childNode.Name, out object deserializer))
                {
                    MethodInfo deserializeMethod = deserializer.GetType().GetMethod("Deserialize");
                    object propertyValue = deserializeMethod.Invoke(deserializer, new object[] { childNode, context });
                }
            }
        }
    }
}
