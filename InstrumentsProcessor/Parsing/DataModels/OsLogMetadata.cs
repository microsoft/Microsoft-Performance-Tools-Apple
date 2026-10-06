// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class OsLogMetadata : IPropertyDeserializer
    {
        // Matches: EventName [key=val, key=val, ...]   OptionalNumericValue
        private static readonly Regex MessagePattern = new Regex(@"^(.+?)\s*\[(.+)\](?:\s+([\d,.]+))?\s*$");

        [CustomDeserialization]
        public string Message { get; private set; }

        [CustomDeserialization]
        public string EventName { get; private set; }

        [CustomDeserialization]
        public Dictionary<string, string> Parameters { get; private set; }

        [CustomDeserialization]
        public double? NumericValue { get; private set; }

        private bool _parsed;
        private string _eventName;
        private Dictionary<string, string> _parameters;
        private double? _numericValue;

        private void ParseMessage(string message)
        {
            if (_parsed) return;
            _parsed = true;

            _parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(message))
            {
                _eventName = string.Empty;
                return;
            }

            Match match = MessagePattern.Match(message);
            if (!match.Success)
            {
                _eventName = message;
                return;
            }

            _eventName = match.Groups[1].Value.Trim();

            // Parse key=value pairs from bracketed section
            string paramsStr = match.Groups[2].Value;
            string[] pairs = paramsStr.Split(new[] { ", " }, StringSplitOptions.None);
            foreach (string pair in pairs)
            {
                int eqIdx = pair.IndexOf('=');
                if (eqIdx >= 0)
                {
                    string key = pair.Substring(0, eqIdx).Trim();
                    string value = pair.Substring(eqIdx + 1);
                    _parameters[key] = value;
                }
            }

            // Parse optional numeric value after the brackets
            if (match.Groups[3].Success)
            {
                string valueStr = match.Groups[3].Value.Replace(",", "");
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double numVal))
                {
                    _numericValue = numVal;
                }
            }

            // Also check for value_ms parameter inside brackets
            if (_numericValue == null && _parameters.TryGetValue("value_ms", out string valueMsStr))
            {
                string cleanValue = valueMsStr.Replace(",", "");
                if (double.TryParse(cleanValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double valueMsNum))
                {
                    _numericValue = valueMsNum;
                }
            }
        }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            string message = node?.Attributes?["fmt"]?.Value ?? string.Empty;
            ParseMessage(message);

            switch (property.Name)
            {
                case "Message":
                    return message;
                case "EventName":
                    return _eventName;
                case "Parameters":
                    return _parameters;
                case "NumericValue":
                    return _numericValue;
                default:
                    throw new InvalidOperationException($"Unknown property: {property.Name}");
            }
        }
    }
}
