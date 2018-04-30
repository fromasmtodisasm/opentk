﻿//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2013 Stefanos Apostolopoulos for the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.XPath;
using Bind.Structures;

namespace Bind
{
    using Delegate = Bind.Structures.Delegate;
    using Enum = Bind.Structures.Enum;

    internal class XmlSpecReader : ISpecReader
    {
        private Settings Settings { get; set; }

        public XmlSpecReader(Settings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void ReadDelegates(string file, DelegateCollection delegates, string apiname, string apiversions)
        {
            var specs = new XPathDocument(file);

            // The pre-GL4.4 spec format does not distinguish between
            // different apinames (it is assumed that different APIs
            // are stored in distinct signature.xml files).
            // To maintain compatibility, we detect the version of the
            // signatures.xml file and ignore apiname if it is version 1.
            var specversion = GetSpecVersion(specs);
            if (specversion == "1")
            {
                apiname = null;
            }

            foreach (var apiversion in apiversions.Split('|'))
            {
                string xpathAdd, xpathDelete;
                GetSignaturePaths(apiname, apiversion, out xpathAdd, out xpathDelete);

                foreach (XPathNavigator nav in specs.CreateNavigator().Select(xpathDelete))
                {
                    foreach (XPathNavigator node in nav.SelectChildren("function", String.Empty))
                    {
                        delegates.Remove(node.GetAttribute("name", String.Empty));
                    }
                }
                foreach (XPathNavigator nav in specs.CreateNavigator().Select(xpathAdd))
                {
                    delegates.AddRange(ReadDelegates(nav, apiversion));
                }
            }
        }

        public void ReadEnums(string file, EnumCollection enums, string apiname, string apiversions)
        {
            var specs = new XPathDocument(file);

            // The pre-GL4.4 spec format does not distinguish between
            // different apinames (it is assumed that different APIs
            // are stored in distinct signature.xml files).
            // To maintain compatibility, we detect the version of the
            // signatures.xml file and ignore apiname if it is version 1.
            var specversion = GetSpecVersion(specs);
            if (specversion == "1")
            {
                apiname = null;
            }

            foreach (var apiversion in apiversions.Split('|'))
            {
                string xpathAdd, xpathDelete;
                GetSignaturePaths(apiname, apiversion, out xpathAdd, out xpathDelete);

                // First, read all enum definitions from spec and override file.
                // Afterwards, read all token/enum overrides from overrides file.
                foreach (XPathNavigator nav in specs.CreateNavigator().Select(xpathDelete))
                {
                    foreach (XPathNavigator node in nav.SelectChildren("enum", String.Empty))
                    {
                        enums.Remove(node.GetAttribute("name", String.Empty));
                    }
                }
                foreach (XPathNavigator nav in specs.CreateNavigator().Select(xpathAdd))
                {
                    Utilities.Merge(enums, ReadEnums(nav));
                }
            }
        }

        public Dictionary<string, string> ReadTypeMap(string file)
        {
            using (var sr = new StreamReader(file))
            {
                Console.WriteLine("Reading opengl types.");
                Dictionary<string, string> glTypes = new Dictionary<string, string>();

                if (sr == null)
                {
                    return glTypes;
                }

                do
                {
                    string line = sr.ReadLine();

                    if (String.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    string[] words = line.Split(" ,*\t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    if (words[0].ToLower() == "void")
                    {
                        // Special case for "void" -> "". We make it "void" -> "void"
                        glTypes.Add(words[0], "void");
                    }
                    else if (words[0] == "VoidPointer" || words[0] == "ConstVoidPointer")
                    {
                        // "(Const)VoidPointer" -> "void*"
                        glTypes.Add(words[0], "void*");
                    }
                    else if (words[0] == "CharPointer" || words[0] == "charPointerARB" ||
                             words[0] == "ConstCharPointer")
                    {
                        // The typematching logic cannot handle pointers to pointers, e.g. CharPointer* -> char** -> string* -> string[].
                        // Hence we give it a push.
                        // Note: When both CurrentType == "String" and Pointer == true, the typematching is hardcoded to use
                        // String[] or StringBuilder[].
                        glTypes.Add(words[0], "String");
                    }
                    /*else if (words[0].Contains("Pointer"))
                    {
                        GLTypes.Add(words[0], words[1].Replace("Pointer", "*"));
                    }*/
                    else if (words[1].Contains("GLvoid"))
                    {
                        glTypes.Add(words[0], "void");
                    }
                    else if (words[1] == "const" && words[2] == "GLubyte")
                    {
                        glTypes.Add(words[0], "String");
                    }
                    else if (words[1] == "struct")
                    {
                        glTypes.Add(words[0], words[2]);
                    }
                    else
                    {
                        glTypes.Add(words[0], words[1]);
                    }
                }
                while (!sr.EndOfStream);

                return glTypes;
            }
        }

        public Dictionary<string, string> ReadCSTypeMap(string file)
        {
            using (var sr = new StreamReader(file))
            {
                Dictionary<string, string> csTypes = new Dictionary<string, string>();
                Console.WriteLine("Reading C# types.");

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (String.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    string[] words = line.Split(" ,\t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length < 2)
                    {
                        continue;
                    }

                    if (((Settings.Compatibility & Settings.Legacy.NoBoolParameters) != Settings.Legacy.None) && words[1] == "bool")
                    {
                        words[1] = "Int32";
                    }

                    csTypes.Add(words[0], words[1]);
                }

                return csTypes;
            }
        }

        private static void GetSignaturePaths(string apiname, string apiversion, out string xpathAdd, out string xpathDelete)
        {
            xpathAdd = "/signatures/add";
            xpathDelete = "/signatures/delete";

            if (!String.IsNullOrEmpty(apiname) && !String.IsNullOrEmpty(apiversion))
            {
                var match = String.Format(
                    "[contains(concat('|', @name, '|'), '|{0}|') and " +
                    "(contains(concat('|', @version, '|'), '|{1}|') or not(boolean(@version)))]",
                    apiname,
                    apiversion);
                xpathAdd += match;
                xpathDelete += match;
            }
            else if (!String.IsNullOrEmpty(apiname))
            {
                var match = String.Format("[contains(concat('|', @name, '|'), '|{0}|')]", apiname);
                xpathAdd += match;
                xpathDelete += match;
            }
        }

        private string GetSpecVersion(XPathDocument specs)
        {
            var version =
                specs.CreateNavigator().SelectSingleNode("/signatures")
                .GetAttribute("version", String.Empty);
            if (String.IsNullOrEmpty(version))
            {
                version = "1";
            }
            return version;
        }

        private DelegateCollection ReadDelegates(XPathNavigator specs, string apiversion)
        {
            DelegateCollection delegates = new DelegateCollection();
            var extensions = new List<string>();

            string path = "function";
            foreach (XPathNavigator node in specs.SelectChildren(path, String.Empty))
            {
                var name = node.GetAttribute("name", String.Empty).Trim();
                var version = node.GetAttribute("version", String.Empty).Trim();

                // Ignore functions that have a higher version number than
                // our current apiversion. Extensions do not have a version,
                // so we add them anyway (which is desirable).
                if (!String.IsNullOrEmpty(version) && !String.IsNullOrEmpty(apiversion) &&
                    Decimal.Parse(version) > Decimal.Parse(apiversion))
                {
                    continue;
                }

                // Check whether we are adding to an existing delegate or creating a new one.
                var d = new Delegate
                {
                    Name = name,
                    EntryPoint = name,
                    Version = node.GetAttribute("version", String.Empty).Trim(),
                    Category = node.GetAttribute("category", String.Empty).Trim(),
                    DeprecatedVersion = node.GetAttribute("deprecated", String.Empty).Trim(),
                    Deprecated = !String.IsNullOrEmpty(node.GetAttribute("deprecated", String.Empty)),
                    Extension = node.GetAttribute("extension", String.Empty).Trim() ?? "Core",
                    Obsolete = node.GetAttribute("obsolete", String.Empty).Trim()
                };
                if (!extensions.Contains(d.Extension))
                {
                    extensions.Add(d.Extension);
                }

                foreach (XPathNavigator param in node.SelectChildren(XPathNodeType.Element))
                {
                    switch (param.Name)
                    {
                        case "returns":
                            d.ReturnType.CurrentType = param.GetAttribute("type", String.Empty).Trim();
                            break;

                        case "param":
                            Parameter p = new Parameter();
                            p.CurrentType = param.GetAttribute("type", String.Empty).Trim();
                            p.Name = param.GetAttribute("name", String.Empty).Trim();

                            p.ComputeSize = param.GetAttribute("count", String.Empty).Trim();

                            int elementCount;
                            if (Int32.TryParse(p.ComputeSize, out elementCount))
                            {
                                p.ElementCount = elementCount;
                            }

                            p.Flow = Parameter.GetFlowDirection(param.GetAttribute("flow", String.Empty).Trim());

                            d.Parameters.Add(p);
                            break;
                    }
                }

                delegates.Add(d);
            }

            Utilities.AddExtensions(extensions);
            return delegates;
        }

        private EnumCollection ReadEnums(XPathNavigator nav)
        {
            EnumCollection enums = new EnumCollection();
            Enum all = new Enum() { Name = Settings.CompleteEnumName };

            if (nav != null)
            {
                var reuseList = new List<KeyValuePair<Enum, string>>();

                // First pass: collect all available tokens and enums
                foreach (XPathNavigator node in nav.SelectChildren("enum", String.Empty))
                {
                    Enum e = new Enum()
                    {
                        Name = node.GetAttribute("name", String.Empty).Trim(),
                        Type = node.GetAttribute("type", String.Empty).Trim()
                    };

                    e.Obsolete = node.GetAttribute("obsolete", String.Empty).Trim();

                    if (String.IsNullOrEmpty(e.Name))
                    {
                        throw new InvalidOperationException(String.Format("Empty name for enum element {0}", node.ToString()));
                    }

                    // It seems that all flag collections contain "Mask" in their names.
                    // This looks like a heuristic, but it holds 100% in practice
                    // (checked all enums to make sure).
                    e.IsFlagCollection = e.Name.ToLower().Contains("mask");

                    foreach (XPathNavigator param in node.SelectChildren(XPathNodeType.Element))
                    {
                        Constant c = null;
                        switch (param.Name)
                        {
                            case "token":
                                c = new Constant
                                {
                                    Name = param.GetAttribute("name", String.Empty).Trim(),
                                    Value = param.GetAttribute("value", String.Empty).Trim()
                                };
                                break;

                            case "use":
                                c = new Constant
                                {
                                    Name = param.GetAttribute("token", String.Empty).Trim(),
                                    Reference = param.GetAttribute("enum", String.Empty).Trim(),
                                    Value = param.GetAttribute("token", String.Empty).Trim(),
                                };
                                break;

                            case "reuse":
                                var reuseEnum = param.GetAttribute("enum", String.Empty).Trim();
                                reuseList.Add(new KeyValuePair<Enum, string>(e, reuseEnum));
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        if (c != null)
                        {
                            Utilities.Merge(all, c);
                            try
                            {
                                if (!e.ConstantCollection.ContainsKey(c.Name))
                                {
                                    e.ConstantCollection.Add(c.Name, c);
                                }
                                else if (e.ConstantCollection[c.Name].Value != c.Value)
                                {
                                    var existing = e.ConstantCollection[c.Name];
                                    if (existing.Reference != null && c.Reference == null)
                                    {
                                        e.ConstantCollection[c.Name] = c;
                                    }
                                    else if (existing.Reference == null && c.Reference != null)
                                    {
                                        // Keep existing
                                    }
                                    else
                                    {
                                        Console.WriteLine("[Warning] Conflicting token {0}.{1} with value {2} != {3}",
                                            e.Name, c.Name, e.ConstantCollection[c.Name].Value, c.Value);
                                    }
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                Console.WriteLine("[Warning] Failed to add constant {0} to enum {1}: {2}", c.Name, e.Name, ex.Message);
                            }
                        }
                    }

                    Utilities.Merge(enums, e);
                }

                // Second pass: resolve "reuse" directives
restart:
                foreach (var pair in reuseList)
                {
                    var e = pair.Key;
                    var reuse = pair.Value;
                    var count = e.ConstantCollection.Count;

                    if (enums.ContainsKey(reuse))
                    {
                        var reuseEnum = enums[reuse];
                        foreach (var token in reuseEnum.ConstantCollection.Values)
                        {
                            Utilities.Merge(e, token);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Warning] Reuse token not found: {0}", reuse);
                    }

                    if (count != e.ConstantCollection.Count)
                    {
                        // Restart resolution of reuse directives whenever
                        // we modify an enum. This is the simplest (brute) way
                        // to resolve chains of reuse directives:
                        // e.g. enum A reuses B which reuses C
                        goto restart;
                    }
                }
            }

            Utilities.Merge(enums, all);
            return enums;
        }
    }
}
