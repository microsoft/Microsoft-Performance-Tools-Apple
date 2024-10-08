// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using InstrumentsProcessor.Parsing.DataModels;
using Microsoft.Performance.SDK.Processing;

// StackDescCPtr type is defined in Microsoft.Performance.PerfCore4.HeapSnapshot
// We use this namespace because PolicyHelper tries to find the policy by attaching 
// full type name + policy(AccessProvider) in the assembly
namespace InstrumentsProcessor.AccessProviders
{
    public struct StackAccessProvider
        : ICollectionAccessProvider<Backtrace, string>,
          ISpecializedKeyComparer<string>
    {
        private const string ByModule = "ByModule";
        private const string ByFunction = "ByFunction";

        private static readonly List<string> Modes = new List<string> { ByModule, ByFunction };
        private static readonly IReadOnlyList<string> ReadOnlyModes = Modes.AsReadOnly();

        public string PastEndValue => "NA";

        public bool HasUniqueStart => false;

        public IReadOnlyList<string> ProjectionModes => ReadOnlyModes;

        public bool Equals(Backtrace x, Backtrace y)
        {
            return x.Equals(y);
        }

        public int GetCount(Backtrace collection)
        {
            return collection.Frames.Count;
        }

        public int GetHashCode(Backtrace x)
        {
            return x.GetHashCode();
        }

        public Backtrace GetParent(Backtrace collection)
        {
            throw new NotImplementedException();
        }

        public IComparer<string> GetSpecializedKeyComparer(string mode)
        {
            throw new NotImplementedException();
        }


        public string GetValue(Backtrace collection, int index)
        {
            if (collection == null )
            {
                return "NA!NA";
            }

            Frame frame;
            int count = collection.Frames.Count;

            if (index >= count)
            {
                frame = collection.Frames[0];

                if (frame == null)
                {
                    return "NA!NA";
                }

                return $"{frame.Module.Name}!{frame.Function.Name}";
            }

            frame = collection.Frames[count-(index+1)];

            if (frame == null)
            {
                return "NA!NA";
            }

            return $"{frame.Module.Name}!{frame.Function.Name}";
        }

        public bool IsNull(Backtrace value)
        {
            if (value == null || value.Frames.Count == 0)
                return true;

            return false;
        }
    }
}
