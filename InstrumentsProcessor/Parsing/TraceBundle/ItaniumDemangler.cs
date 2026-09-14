// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Minimal Itanium ABI C++ name demangler. Handles the mangling patterns used by
// modern Clang/LLVM (matching most symbols in Microsoft Edge / Chromium / V8 /
// libc++), with best-effort output for constructs that require deep template /
// substitution reasoning.
//
// Grammar reference: https://itanium-cxx-abi.github.io/cxx-abi/abi.html#mangling
//
// Design goals:
//   * Self-contained (no native / P/Invoke dependency).
//   * Fast (called for every stack frame in a WPA table).
//   * Safe: on any parse failure return the original mangled string unchanged.
//   * Readable: prefer legibility over strict ABI compliance for edge cases.

using System;
using System.Collections.Generic;
using System.Text;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal static class ItaniumDemangler
    {
        /// <summary>
        /// Try to demangle a mangled C++ symbol name. If <paramref name="mangled"/>
        /// is not an Itanium-mangled name, returns it unchanged. Trailing CFI/ICF
        /// hash suffixes (e.g. <c>.b18c00d3a73a12ff...</c>) and leading Mach-O
        /// underscores are handled transparently.
        /// </summary>
        public static string TryDemangle(string mangled)
        {
            if (string.IsNullOrEmpty(mangled)) return mangled;

            // Strip trailing LLVM/Clang tail suffixes (ICF/CFI hash, .llvm.N, .cold, etc.)
            string suffix = string.Empty;
            int dotIdx = mangled.IndexOf('.');
            string body = mangled;
            if (dotIdx > 0)
            {
                suffix = mangled.Substring(dotIdx);
                body = mangled.Substring(0, dotIdx);
            }

            // Mach-O prepends an underscore to every C symbol, so C++ symbols
            // arrive as "__Z..." instead of "_Z...".
            if (body.Length > 1 && body[0] == '_' && body[1] == '_' && body.Length > 2 && body[2] == 'Z')
            {
                body = body.Substring(1);
            }

            if (!body.StartsWith("_Z", StringComparison.Ordinal))
                return mangled; // not an Itanium-mangled name

            var p = new Parser(body, 2);
            string demangled;
            try
            {
                demangled = p.ParseEncoding();
            }
            catch
            {
                return mangled;
            }

            if (string.IsNullOrEmpty(demangled))
                return mangled;

            return demangled + suffix;
        }

        // ─── Parser state ────────────────────────────────────────────────

        private sealed class Parser
        {
            private readonly string _s;
            private int _pos;

            /// <summary>
            /// Substitution table: previously-parsed types and prefix components.
            /// Referenced by <c>S_</c>, <c>S0_</c>, <c>S1_</c>, etc. In Itanium ABI:
            /// * S_ = table[0]
            /// * S0_ = table[1]
            /// * S1_ = table[2]
            /// * S{N}_ where N is base-36 (0-9, A-Z) means table[N+1].
            /// </summary>
            private readonly List<string> _subs = new List<string>();

            public Parser(string s, int start) { _s = s; _pos = start; }

            // ── Convenience helpers ───────────────────────────────────

            private char Peek(int off = 0) =>
                _pos + off < _s.Length ? _s[_pos + off] : '\0';

            private char Take()
            {
                if (_pos >= _s.Length) throw new FormatException("unexpected end");
                return _s[_pos++];
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw new FormatException($"expected '{c}' at {_pos}");
                _pos++;
            }

            private bool AtEnd => _pos >= _s.Length;

            // ── Top-level entry ───────────────────────────────────────

            public string ParseEncoding()
            {
                // <encoding> ::= <name> [<bare-function-type>]
                //              | <special-name>
                string name = ParseName();

                // If more input remains, it's usually the bare-function-type
                // (parameter list). Best-effort parse; on failure just drop
                // the parameters — the function name is what matters most.
                if (!AtEnd)
                {
                    var paramList = TryParseFunctionParams();
                    if (paramList != null)
                        name += "(" + string.Join(", ", paramList) + ")";
                }

                return name;
            }

            // ── <name> ────────────────────────────────────────────────

            private string ParseName()
            {
                switch (Peek())
                {
                    case 'N': return ParseNestedName();
                    case 'S': return ParseSubstitutionOrStdName();
                    case 'Z': return ParseLocalName();
                    default:
                        if (char.IsDigit(Peek()))
                        {
                            // <unscoped-name> ::= <source-name>
                            // may be followed by <template-args>
                            string name = ParseSourceName();
                            AddSub(name);
                            if (Peek() == 'I')
                            {
                                string tArgs = ParseTemplateArgs();
                                name += tArgs;
                                AddSub(name);
                            }
                            return name;
                        }
                        throw new FormatException($"unrecognised name at {_pos}");
                }
            }

            private string ParseNestedName()
            {
                // N [<CV-qualifiers>] [<ref-qualifier>] <prefix> <unqualified-name> E
                //   | N [<CV>] [<ref>] <template-prefix> <template-args> E
                Expect('N');

                // CV qualifiers: any sequence of r/V/K (restrict/volatile/const)
                while (Peek() == 'r' || Peek() == 'V' || Peek() == 'K') _pos++;
                // Ref qualifier: R (lvalue) or O (rvalue)
                if (Peek() == 'R' || Peek() == 'O') _pos++;

                var parts = new List<string>();
                while (Peek() != 'E' && !AtEnd)
                {
                    string part = ParsePrefixComponent(parts);
                    if (part == null) break;
                    // If the next thing is template args, they belong to the
                    // component we just parsed.
                    if (Peek() == 'I')
                    {
                        string tArgs = ParseTemplateArgs();
                        part += tArgs;
                    }
                    parts.Add(part);
                    // Register the *full* qualified name so far in the sub table.
                    AddSub(string.Join("::", parts));
                }
                Expect('E');

                return string.Join("::", parts);
            }

            /// <summary>
            /// Parse one component of a nested name (a piece between "::").
            /// Handles substitutions, ctor/dtor sigils, operator names, and
            /// source names.
            /// </summary>
            private string ParsePrefixComponent(List<string> partsSoFar)
            {
                char c = Peek();

                if (c == 'S') return ParseSubstitutionOrStdName();

                if (c == 'C') return ParseCtorName(partsSoFar);
                if (c == 'D') return ParseDtorName(partsSoFar);

                if (c == 'L' && char.IsDigit(Peek(1)))
                {
                    _pos++; // L = local/internal linkage marker, skip
                    return ParseSourceName();
                }

                if (char.IsDigit(c)) return ParseSourceName();

                // Operator names: on / cl / dv / ml / etc. Best-effort mapping.
                if (char.IsLower(c) && char.IsLetter(Peek(1)))
                {
                    string op = ParseOperatorName();
                    if (op != null) return op;
                }

                return null;
            }

            /// <summary>Parse a length-prefixed source name (e.g. <c>15edge_continuity</c>).</summary>
            private string ParseSourceName()
            {
                int len = 0;
                while (char.IsDigit(Peek()))
                {
                    len = len * 10 + (Take() - '0');
                    if (len > _s.Length - _pos) throw new FormatException("source-name length overrun");
                }
                if (len == 0) throw new FormatException("empty source-name");
                if (_pos + len > _s.Length) throw new FormatException("source-name truncated");
                string name = _s.Substring(_pos, len);
                _pos += len;

                // The anonymous-namespace marker is a specially-mangled identifier.
                if (name.StartsWith("_GLOBAL__N_", StringComparison.Ordinal))
                    return "(anonymous namespace)";
                return name;
            }

            // ── Ctor / dtor / operator names ─────────────────────────

            private string ParseCtorName(List<string> partsSoFar)
            {
                // C1 / C2 / C3 / C4 / C5 -> render as ClassName
                Expect('C');
                char v = Take();
                if (v < '1' || v > '5') throw new FormatException("bad ctor tag");
                return partsSoFar.Count > 0 ? partsSoFar[partsSoFar.Count - 1] : "ctor";
            }

            private string ParseDtorName(List<string> partsSoFar)
            {
                // D0 / D1 / D2 -> render as ~ClassName
                Expect('D');
                char v = Take();
                if (v < '0' || v > '5') throw new FormatException("bad dtor tag");
                string cls = partsSoFar.Count > 0 ? partsSoFar[partsSoFar.Count - 1] : "dtor";
                return "~" + cls;
            }

            private static readonly Dictionary<string, string> Operators = new Dictionary<string, string>
            {
                { "nw", "operator new" },  { "na", "operator new[]" },
                { "dl", "operator delete" }, { "da", "operator delete[]" },
                { "ps", "operator+" }, { "ng", "operator-" },
                { "ad", "operator&" }, { "de", "operator*" },
                { "co", "operator~" }, { "pl", "operator+" },
                { "mi", "operator-" }, { "ml", "operator*" },
                { "dv", "operator/" }, { "rm", "operator%" },
                { "an", "operator&" }, { "or", "operator|" },
                { "eo", "operator^" }, { "aS", "operator=" },
                { "pL", "operator+=" }, { "mI", "operator-=" },
                { "mL", "operator*=" }, { "dV", "operator/=" },
                { "rM", "operator%=" }, { "aN", "operator&=" },
                { "oR", "operator|=" }, { "eO", "operator^=" },
                { "ls", "operator<<" }, { "rs", "operator>>" },
                { "lS", "operator<<=" }, { "rS", "operator>>=" },
                { "eq", "operator==" }, { "ne", "operator!=" },
                { "lt", "operator<" }, { "gt", "operator>" },
                { "le", "operator<=" }, { "ge", "operator>=" },
                { "nt", "operator!" }, { "aa", "operator&&" },
                { "oo", "operator||" }, { "pp", "operator++" },
                { "mm", "operator--" }, { "cm", "operator," },
                { "pm", "operator->*" }, { "pt", "operator->" },
                { "cl", "operator()" }, { "ix", "operator[]" },
                { "qu", "operator?" }, { "st", "sizeof" },
                { "sz", "sizeof" },
            };

            private string ParseOperatorName()
            {
                if (_pos + 2 > _s.Length) return null;
                string op = _s.Substring(_pos, 2);
                if (!Operators.TryGetValue(op, out string mapped)) return null;
                _pos += 2;
                return mapped;
            }

            // ── Substitutions & std:: shorthand ──────────────────────

            private string ParseSubstitutionOrStdName()
            {
                Expect('S');
                char c = Peek();

                // Standard substitutions
                switch (c)
                {
                    case 't': _pos++; return ParseStdSubBody();
                    case 'a': _pos++; return "std::allocator";
                    case 'b': _pos++; return "std::basic_string";
                    case 's': _pos++; return "std::string";
                    case 'i': _pos++; return "std::basic_istream";
                    case 'o': _pos++; return "std::basic_ostream";
                    case 'd': _pos++; return "std::basic_iostream";
                }

                // Numeric / seq-id substitution: S_, S0_, S1_, ..., SA_, ...
                int seq = 0;
                if (c == '_')
                {
                    _pos++;
                    return LookupSub(0);
                }
                while (Peek() != '_')
                {
                    if (AtEnd) throw new FormatException("truncated substitution");
                    int digit = Base36Value(Take());
                    if (digit < 0) throw new FormatException("bad seq-id char");
                    seq = seq * 36 + digit;
                }
                Expect('_');
                return LookupSub(seq + 1);
            }

            /// <summary>
            /// After <c>St</c>: parse an unqualified name (possibly with template args)
            /// in the <c>std::</c> namespace.
            /// </summary>
            private string ParseStdSubBody()
            {
                // St is a shorthand for "std::"; what follows is an unqualified name
                // and possibly template args. Behave like a nested-name of ["std", <name>].
                if (char.IsDigit(Peek()))
                {
                    string body = ParseSourceName();
                    string full = "std::" + body;
                    AddSub(full);
                    if (Peek() == 'I')
                    {
                        full += ParseTemplateArgs();
                        AddSub(full);
                    }
                    return full;
                }
                // Not a source-name — fall back
                return "std";
            }

            private static int Base36Value(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'A' && c <= 'Z') return 10 + (c - 'A');
                return -1;
            }

            private string LookupSub(int idx)
            {
                if (idx < 0 || idx >= _subs.Count)
                    throw new FormatException($"sub #{idx} out of range (have {_subs.Count})");
                return _subs[idx];
            }

            private void AddSub(string s)
            {
                _subs.Add(s);
            }

            // ── Template args ────────────────────────────────────────

            private string ParseTemplateArgs()
            {
                Expect('I');
                var args = new List<string>();
                while (Peek() != 'E' && !AtEnd)
                {
                    string arg = ParseTemplateArg();
                    args.Add(arg);
                }
                Expect('E');
                return "<" + string.Join(", ", args) + ">";
            }

            private string ParseTemplateArg()
            {
                char c = Peek();
                if (c == 'L') return ParseExprPrimary();
                if (c == 'X') { /* expression */ _pos++; SkipUntilE(); return "?"; }
                if (c == 'J')
                {
                    // template arg pack
                    _pos++;
                    var packed = new List<string>();
                    while (Peek() != 'E' && !AtEnd) packed.Add(ParseTemplateArg());
                    Expect('E');
                    return string.Join(", ", packed);
                }
                return ParseType();
            }

            private string ParseExprPrimary()
            {
                // L <type> <value> E
                Expect('L');
                string typeStr = ParseType();
                var sb = new StringBuilder();
                while (Peek() != 'E' && !AtEnd) sb.Append(Take());
                Expect('E');
                return sb.ToString();
            }

            private void SkipUntilE()
            {
                int depth = 1;
                while (!AtEnd && depth > 0)
                {
                    char c = Take();
                    if (c == 'X' || c == 'I' || c == 'N') depth++;
                    else if (c == 'E') depth--;
                }
            }

            // ── Types ────────────────────────────────────────────────

            private static readonly Dictionary<char, string> BuiltinTypes = new Dictionary<char, string>
            {
                { 'v', "void" }, { 'w', "wchar_t" }, { 'b', "bool" },
                { 'c', "char" }, { 'a', "signed char" }, { 'h', "unsigned char" },
                { 's', "short" }, { 't', "unsigned short" },
                { 'i', "int" }, { 'j', "unsigned int" },
                { 'l', "long" }, { 'm', "unsigned long" },
                { 'x', "long long" }, { 'y', "unsigned long long" },
                { 'n', "__int128" }, { 'o', "unsigned __int128" },
                { 'f', "float" }, { 'd', "double" }, { 'e', "long double" },
                { 'g', "__float128" }, { 'z', "..." },
                { 'D', "" }, // handled specially: Dn / Di / Ds / Da
            };

            private string ParseType()
            {
                char c = Peek();

                // Substitution?
                if (c == 'S')
                {
                    int saved = _pos;
                    string subOrStd = ParseSubstitutionOrStdName();
                    // A substitution used as a type may be followed by template args.
                    if (Peek() == 'I')
                    {
                        string t = ParseTemplateArgs();
                        subOrStd += t;
                        AddSub(subOrStd);
                    }
                    return subOrStd;
                }

                // Nested-name as a type (class / enum / template)
                if (c == 'N')
                {
                    string n = ParseNestedName();
                    return n;
                }

                // Source name as a type
                if (char.IsDigit(c))
                {
                    string sn = ParseSourceName();
                    AddSub(sn);
                    if (Peek() == 'I')
                    {
                        string t = ParseTemplateArgs();
                        sn += t;
                        AddSub(sn);
                    }
                    return sn;
                }

                // Qualifiers (const / volatile / restrict) that decorate a following type
                if (c == 'K' || c == 'V' || c == 'r')
                {
                    _pos++;
                    string inner = ParseType();
                    string qual = c switch { 'K' => "const", 'V' => "volatile", _ => "restrict" };
                    string full = inner + " " + qual;
                    AddSub(full);
                    return full;
                }

                // Pointer / reference / rvalue-reference
                if (c == 'P') { _pos++; string inner = ParseType(); string full = inner + "*"; AddSub(full); return full; }
                if (c == 'R') { _pos++; string inner = ParseType(); string full = inner + "&"; AddSub(full); return full; }
                if (c == 'O') { _pos++; string inner = ParseType(); string full = inner + "&&"; AddSub(full); return full; }
                if (c == 'C') { _pos++; string inner = ParseType(); return inner + " complex"; }
                if (c == 'G') { _pos++; string inner = ParseType(); return inner + " imaginary"; }

                // Array
                if (c == 'A')
                {
                    _pos++;
                    var dim = new StringBuilder();
                    while (Peek() != '_' && !AtEnd) dim.Append(Take());
                    Expect('_');
                    string inner = ParseType();
                    return $"{inner}[{dim}]";
                }

                // Function type: F ... E
                if (c == 'F')
                {
                    _pos++;
                    // optional Y (extern "C")
                    if (Peek() == 'Y') _pos++;
                    string ret = ParseType();
                    var args = new List<string>();
                    while (Peek() != 'E' && !AtEnd) args.Add(ParseType());
                    Expect('E');
                    string full = $"{ret} ({string.Join(", ", args)})";
                    AddSub(full);
                    return full;
                }

                // Template param: T_, T0_, T1_ ...
                if (c == 'T')
                {
                    _pos++;
                    if (Peek() == '_') { _pos++; return "T"; }
                    var buf = new StringBuilder();
                    while (Peek() != '_' && !AtEnd) buf.Append(Take());
                    Expect('_');
                    return "T" + buf;
                }

                // Vendor-extended qualifier: U <source-name> <type>
                if (c == 'U')
                {
                    _pos++;
                    string qname = char.IsDigit(Peek()) ? ParseSourceName() : "";
                    string inner = ParseType();
                    return inner + " " + qname;
                }

                // 'D' prefix: various — Dn (nullptr_t), Ds (char16), Di (char32), Da (auto), Dc (decltype(auto)), Dt/DT (decltype)
                if (c == 'D')
                {
                    _pos++;
                    char d = Take();
                    switch (d)
                    {
                        case 'n': return "std::nullptr_t";
                        case 'i': return "char32_t";
                        case 's': return "char16_t";
                        case 'a': return "auto";
                        case 'c': return "decltype(auto)";
                        case 'h': return "half";
                        case 'F': return "fixed";  // approximate
                        case 'v': /* Dv: vector — skip parameters */
                            {
                                // Dv <number> _ <type> | Dv _ <expr> _ <type>
                                while (Peek() != '_' && !AtEnd) _pos++;
                                if (Peek() == '_') _pos++;
                                return ParseType() + " vector";
                            }
                        case 't':
                        case 'T':
                            {
                                // decltype (expression) — skip until E
                                SkipUntilE();
                                return "decltype(...)";
                            }
                        default:
                            return "D" + d;
                    }
                }

                // Builtin scalar types
                if (BuiltinTypes.TryGetValue(c, out string name))
                {
                    _pos++;
                    return name;
                }

                throw new FormatException($"unknown type char '{c}' at {_pos}");
            }

            // ── Local names ──────────────────────────────────────────

            private string ParseLocalName()
            {
                // Z <function encoding> E <entity name> [_ <discriminator>]
                Expect('Z');
                string outer = ParseEncoding();
                Expect('E');
                // Entity: either <name> or a special 's' (string literal) or 'd' (default arg).
                string inner;
                if (Peek() == 's') { _pos++; inner = "\"string literal\""; }
                else if (Peek() == 'd')
                {
                    _pos++;
                    // Optional param number then _
                    while (Peek() != '_' && !AtEnd) _pos++;
                    if (Peek() == '_') _pos++;
                    inner = ParseName();
                }
                else
                {
                    inner = ParseName();
                }
                // Optional discriminator: _ N (single digit) or __ NN_ (multi-digit)
                if (Peek() == '_')
                {
                    _pos++;
                    while (char.IsDigit(Peek())) _pos++;
                }
                return outer + "::" + inner;
            }

            // ── Function parameters (best effort) ────────────────────

            private List<string> TryParseFunctionParams()
            {
                int savedPos = _pos;
                int savedSubCount = _subs.Count;
                var result = new List<string>();
                try
                {
                    while (!AtEnd)
                    {
                        string t = ParseType();
                        result.Add(t);
                    }
                    // Collapse a single "void" parameter to nothing.
                    if (result.Count == 1 && result[0] == "void") result.Clear();
                    return result;
                }
                catch
                {
                    // Roll back on failure so ParseEncoding returns just the name.
                    _pos = savedPos;
                    while (_subs.Count > savedSubCount) _subs.RemoveAt(_subs.Count - 1);
                    return null;
                }
            }
        }
    }
}
