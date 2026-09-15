// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal static class ItaniumDemangler
    {
        // TODO: Add verified demangling; preserve recorded names until then.
        public static string TryDemangle(string mangled) => mangled;
    }
}