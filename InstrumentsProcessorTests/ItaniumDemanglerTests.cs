// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.TraceBundle;
using Xunit;

namespace InstrumentsProcessorTests
{
    public class ItaniumDemanglerTests
    {
        [Theory]
        // Pass-through: not mangled
        [InlineData("printf", "printf")]
        [InlineData("", "")]
        [InlineData("_main", "_main")]

        // Simple unscoped names
        [InlineData("_Z3fooi", "foo(int)")]
        [InlineData("_Z3fooii", "foo(int, int)")]
        [InlineData("_Z3foov", "foo()")] 

        // Nested names
        [InlineData("_ZN3foo3barEv", "foo::bar()")] 

        // Mach-O double-underscore prefix
        [InlineData("__Z3fooi", "foo(int)")]

        // Trailing LLVM CFI/ICF hash — preserved verbatim
        [InlineData("__Z3fooi.b18c00d3a73a12ff00b55be6c2eff04a",
                    "foo(int).b18c00d3a73a12ff00b55be6c2eff04a")]

        // Constructor / destructor tags
        [InlineData("_ZN3fooC1Ev", "foo::foo()")] 
        [InlineData("_ZN3fooD0Ev", "foo::~foo()")] 
        [InlineData("_ZN3fooD1Ev", "foo::~foo()")] 

        // Reference type parameter
        [InlineData("_ZN15edge_continuity12string_utils22GetLocalizedStringDictERN4base9DictValueE",
                    "edge_continuity::string_utils::GetLocalizedStringDict(base::DictValue&)")]

        // Anonymous namespace
        [InlineData("_ZN12_GLOBAL__N_134PinningWizardWebContentsDriverImpl24FaviconDriverEventRouterD0Ev",
                    "(anonymous namespace)::PinningWizardWebContentsDriverImpl::FaviconDriverEventRouter::~FaviconDriverEventRouter()")]

        // Real Chromium samples (verify no crash + basic legibility)
        [InlineData("_ZN2v88internal19SwissNameDictionary16EqualsForTestingENS0_6TaggedIS1_EE",
                    "v8::internal::SwissNameDictionary::EqualsForTesting(v8::internal::Tagged<v8::internal::SwissNameDictionary>)")]

        public void Demangle_ProducesExpected(string mangled, string expected)
        {
            string got = ItaniumDemangler.TryDemangle(mangled);
            Assert.Equal(expected, got);
        }

        [Theory]
        // Complex samples: verify at least the qualified name portion is legible
        // (parameters may vary in fidelity; test only the prefix through the ')').
        [InlineData(
            "_ZNSt4__Cr6vectorIN6syncer18UpdateResponseDataENS_9allocatorIS2_EEE5eraseENS_11__wrap_iterIPKS2_EES9_",
            "std::__Cr::vector<syncer::UpdateResponseData")]
        public void Demangle_StartsWith(string mangled, string expectedPrefix)
        {
            string got = ItaniumDemangler.TryDemangle(mangled);
            Assert.StartsWith(expectedPrefix, got);
        }

        [Fact]
        public void Demangle_ReturnsOriginal_OnFailure()
        {
            // Malformed but starts with _Z
            string got = ItaniumDemangler.TryDemangle("_Zthis_is_not_valid_at_all");
            Assert.Equal("_Zthis_is_not_valid_at_all", got);
        }
    }
}