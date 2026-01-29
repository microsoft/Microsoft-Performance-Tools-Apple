// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.Events
{
    using PropertyDeserializer = Func<XmlNode, XmlParsingContext, object>;
    using PropertySetter = Action<object, XmlNode, XmlParsingContext>;

    internal class DeserializerMap
    {
        public DeserializerMap(Dictionary<string, PropertyDeserializer> byPropertyName, Dictionary<string, PropertyDeserializer> byEngineeringType)
        {
            ByPropertyName = byPropertyName;
            ByEngineeringType = byEngineeringType;
        }

        public readonly Dictionary<string, PropertyDeserializer> ByPropertyName;
        public readonly Dictionary<string, PropertyDeserializer> ByEngineeringType;
    };

    /// <summary>
    /// The EventDeserializer class builds deserializers for events, which represent rows of data in a schema.
    /// It uses the ColumnAttribute to identify properties in the event and creates deserializers for each using XmlNodeDeserializer.
    /// When deserializing, it ensures the event properties match the schema's column names and engineering types.
    /// If they match, it deserializes the XML node's child nodes in order, assigning them to the event's properties.
    /// </summary>
    public class EventDeserializer<TEvent> : IEventDeserializer where TEvent : Event, new()
    {
        private PropertySetter[] settersInOrder;
        private static readonly Dictionary<(string Name, string EngineeringType), PropertyInfo> propertiesByColumn = CollectProperties();
        private static readonly DeserializerMap deserializers = MakeDeserializers();

        private static Dictionary<(string Name, string EngineeringType), PropertyInfo> CollectProperties()
        {
            var propertyMap = new Dictionary<(string Name, string EngineeringType), PropertyInfo>();

            PropertyInfo[] properties = typeof(TEvent).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                ColumnAttribute attribute = property.GetCustomAttribute<ColumnAttribute>();

                if (attribute != null)
                {
                    propertyMap[(attribute.Name, attribute.EngineeringType)] = property;
                }
            }

            return propertyMap;
        }

        private static DeserializerMap MakeDeserializers()
        {
            var byPropertyName = new Dictionary<string, PropertyDeserializer>();
            var byEngineeringType = new Dictionary<string, PropertyDeserializer>();

            foreach (var entry in propertiesByColumn)
            {
                Type propertyType = entry.Value.PropertyType;

                if (propertyType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"Property {entry.Key.Name} of type {propertyType.FullName} must have a public parameterless constructor.");
                }

                Type deserializerType = typeof(XmlNodeDeserializer<>).MakeGenericType(propertyType);
                object deserializer = Activator.CreateInstance(deserializerType);
                MethodInfo deserializeMethod = deserializer.GetType().GetMethod("Deserialize");

                PropertyDeserializer callDeserializer = (node, context) =>
                {
                    return deserializeMethod.Invoke(deserializer, new object[] { node, context });
                };

                byPropertyName[entry.Value.Name] = callDeserializer;
                byEngineeringType[entry.Key.EngineeringType] = callDeserializer;
            }

            return new DeserializerMap(byPropertyName, byEngineeringType);
        }

        public bool CanDeserialize(Schema schema)
        {
            // Take this opportunity to index our properties & deserializers by schema order.
            // We don't expect Deserialize() to be called unless we return true.
            settersInOrder = new PropertySetter[schema.Columns.Count];

            for (int i = 0; i < schema.Columns.Count; i++)
            {
                Schema.Column column = schema.Columns[i];
                if (!propertiesByColumn.TryGetValue((column.Name, column.EngineeringType), out PropertyInfo property))
                {
                    return false;
                }

                if (!deserializers.ByPropertyName.TryGetValue(property.Name, out PropertyDeserializer deserializer))
                {
                    return false;
                }

                settersInOrder[i] = (eventInstance, childNode, context) =>
                {
                    property.SetValue(eventInstance, deserializer(childNode, context));
                };
            }

            return true;
        }

        public Event Deserialize(XmlNode node, XmlParsingContext context)
        {
            if (node.ChildNodes.Count != settersInOrder.Length)
            {
                throw new InvalidOperationException($"The number of child nodes in the XML ({node.ChildNodes.Count}) does not match the number of columns in the schema ({settersInOrder.Length}).");
            }

            TEvent instance = new TEvent();

            for (int i = 0; i < settersInOrder.Length; i++)
            {
                XmlNode childNode = node.ChildNodes[i];
                settersInOrder[i](instance, childNode, context);

                if (childNode.Name == "narrative" || childNode.Name == "formatted-label")
                {
                    // Try to deserialize top level nodes in dynamic types so we don't miss object definitions
                    ProcessDynamicType(childNode, context);
                }
            }

            return instance;
        }

        private void ProcessDynamicType(XmlNode dynamicNode, XmlParsingContext context)
        {
            foreach (XmlNode childNode in dynamicNode)
            {
                if (deserializers.ByEngineeringType.TryGetValue(childNode.Name, out PropertyDeserializer deserialize))
                {
                    deserialize(childNode, context);
                }
            }
        }
    }
}
