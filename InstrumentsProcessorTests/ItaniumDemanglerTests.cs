// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.TraceBundle;
using Xunit;

namespace InstrumentsProcessorTests
{
    public class ItaniumDemanglerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("printf")]
        [InlineData("_main")]
        [InlineData("__CFDataDeallocate")]
        [InlineData("_Z3fooi")]
        [InlineData("__Z3fooi")]
        [InlineData("__Z3fooi.b18c00d3a73a12ff00b55be6c2eff04a")]
        [InlineData("__ZL21CGPDFDocumentFinalizePKv")]
        [InlineData("__ZNK10__cxxabiv120__si_class_type_info16search_above_dstEPNS_19__dynamic_cast_infoEPKvS4_ib")]
        [InlineData("_$s6Module3fooyyF")]
        [InlineData("function(int) const")]
        [InlineData("_Zthis_is_not_valid_at_all")]
        public void DemanglingHookPreservesStoredName(string? name)
        {
            Assert.Equal(name, ItaniumDemangler.TryDemangle(name!));
        }
    }
}