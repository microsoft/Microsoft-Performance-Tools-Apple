// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Backtrace : IPropertyDeserializer
    {
        private static XmlNodeDeserializer<Frame> FrameDeserializer = new XmlNodeDeserializer<Frame>();

        [CustomDeserialization]
        public virtual IReadOnlyList<Frame> Frames { get; private set; }

        public Backtrace() { }
        internal Backtrace(IReadOnlyList<Frame> frames) { Frames = frames; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Frames")
            {
                List<Frame> frames = new List<Frame>();

                // tagged-backtrace wraps a backtrace element plus additional data (e.g. uint64).
                // Unwrap to the inner backtrace node that contains the frame elements.
                XmlNode framesParent = node;
                if (node.Name == "tagged-backtrace")
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name == "backtrace")
                        {
                            framesParent = child;
                            break;
                        }
                    }
                }

                foreach (XmlNode childNode in framesParent)
                {
                    frames.Add(FrameDeserializer.Deserialize(childNode, context));
                }

                return frames;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }

    /// <summary>
    /// A memory-efficient backtrace that stores only the raw address array
    /// and materializes Frame objects lazily on first access.
    /// When a SymbolCatalog is available, resolves addresses to image+offset.
    /// For traces with millions of samples, this avoids allocating Frame/Function
    /// objects for backtraces that are never expanded by the user.
    /// </summary>
    internal sealed class LazyBacktrace : Backtrace
    {
        private readonly ulong[] _addresses;
        private readonly TraceBundle.SymbolCatalog _symbols;
        private readonly TraceBundle.SymbolContext _symbolContext;
        private IReadOnlyList<Frame> _frames;

        internal LazyBacktrace(ulong[] addresses, TraceBundle.SymbolCatalog symbols = null,
            TraceBundle.SymbolContext symbolContext = null)
        {
            _addresses = addresses;
            _symbols = symbols;
            _symbolContext = symbolContext;
        }

        public override IReadOnlyList<Frame> Frames
        {
            get
            {
                return System.Threading.LazyInitializer.EnsureInitialized(ref _frames, () =>
                {
                    if (_addresses == null || _addresses.Length == 0)
                    {
                        return Array.Empty<Frame>();
                    }
                    else if (_symbols != null)
                    {
                        return _symbols.ResolveBacktrace(_addresses, _symbolContext);
                    }
                    else
                    {
                        var frames = new Frame[_addresses.Length];
                        for (int i = 0; i < _addresses.Length; i++)
                        {
                            frames[i] = new Frame(new Function(null, $"0x{_addresses[i]:x}"));
                        }
                        return frames;
                    }
                });
            }
        }
    }
}
