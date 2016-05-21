using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Collections;
using CoreLib.Management;
using System.Reflection;
using Microsoft.CSharp;
using System.CodeDom;
using System.Data;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System;

using CoreLib.Conversion;
using CoreLib.Generic;
using CoreLib.Runtime;
using CoreLib;

namespace RunDLL
{
    /// <summary>
    /// The application's static class
    /// </summary>
    public unsafe static class Program
    {
        internal static readonly Regex REGEX_TYPE = new Regex(@"(?<namespace>(\w+\.)*)(?<class>\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static readonly Regex REGEX_METHOD = new Regex(@"((?<namespace>(\w+\.)*)(?<class>\w+\.))?(?<name>\w+)(?<parameters>\(\s*(\s*\,?\s*((ref|out|in)\s+|\&\s*)?(\w+\.)*\w+(\s*((\[(\,)*\])+|\*+))?)+\s*\))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static readonly Dictionary<string, Tuple<string, string>> PRIMITIVES = new Dictionary<string, Tuple<string, string>>() {
            { "bool", new Tuple<string, string>("System", "Boolean") },
            { "byte", new Tuple<string, string>("System", "Byte") },
            { "sbyte", new Tuple<string, string>("System", "SByte") },
            { "short", new Tuple<string, string>("System", "Int16") },
            { "ushort", new Tuple<string, string>("System", "UInt16") },
            { "char", new Tuple<string, string>("System", "Char") },
            { "int", new Tuple<string, string>("System", "Int32") },
            { "uint", new Tuple<string, string>("System", "UInt32") },
            { "long", new Tuple<string, string>("System", "Int64") },
            { "ulong", new Tuple<string, string>("System", "UInt64") },
            { "float", new Tuple<string, string>("System", "Single") },
            { "single", new Tuple<string, string>("System", "Single") },
            { "double", new Tuple<string, string>("System", "Double") },
            { "decimal", new Tuple<string, string>("System", "Decimal") },
            { "dynamic", new Tuple<string, string>("System", "Object") },
            { "void", new Tuple<string, string>("System", "Void") },
            { "object", new Tuple<string, string>("System", "Object") },
            { "string", new Tuple<string, string>("System", "String") },
        };
        internal static readonly FileInfo nfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
        internal static readonly string modname = nfo.Name.ToLower().Replace(nfo.Extension.ToLower(), "").Trim('.', ' ', '\t', '\r', '\n');
        internal static readonly string helpstr = "Use '" + modname + " --help' for further reference.";
        internal static IEnumerable<Type> alltypes;
        internal static AssemblyName asmname;
        internal static Assembly targasm;
        internal static FileInfo targnfo;


        /// <summary>
        /// Static constructor
        /// </summary>
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((o, a) => {
                if (a.Name.ToLower().Contains("uclib"))
                    return Assembly.Load(Properties.Resources.uclib);
                else
                    return null;
            });
        }

        /// <summary>
        /// The application's entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        public static int Main(string[] args)
        {
            #region WARMUP / PREJIT

            typeof(Assembly).GetMethod("GetReferencedAssemblies").WarmUp();

            foreach (MethodInfo nfo in typeof(IEnumerable<>).GetMethods())
                if (!nfo.IsAbstract)
                    nfo.WarmUp();

            #endregion
            #region HELP AND BASIC ARGUMENT CHECKS

            if (CheckHelp(args))
                return 0;
            else if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid argument count.{0}", helpstr);
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            #endregion
            #region FILE EXISTENCE + ASM LOADING

            targnfo = new FileInfo(args[0]);

            if (!targnfo.Exists)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The assembly `{0}` could not not be found.", targnfo);
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            try
            {
                targasm = Assembly.LoadFrom(targnfo.FullName);
                asmname = targasm.GetName();

                Module manmodule = targasm.ManifestModule;

                bool verbose = (from string argv in args
                                let targv = argv.Trim().ToLower()
                                where targv == "--verbose" ||
                                      targv == "--v"
                                select false).Count() > 0;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Assembly loaded:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("    Name:           {0}", asmname.Name);
                Console.WriteLine("    Version:        {0}", asmname.Version);
                Console.WriteLine("    Full name:      {0}", asmname.FullName);

                if (verbose)
                    Console.WriteLine("    Public key:     {0}", string.Join("", from b in asmname.GetPublicKey() ?? new byte[0] select b.ToString("X2")));

                Console.WriteLine("    Architecture:   {0}", asmname.ProcessorArchitecture);

                if (verbose)
                    Console.WriteLine("    Culture name:   {0}", asmname.CultureName ?? "??-??");

                Console.WriteLine("    Assembly flags: {0}", asmname.Flags);
                Console.WriteLine("    CLR version:    {0}", targasm.ImageRuntimeVersion);
                Console.WriteLine("    Cached in GAC:  {0}", targasm.GlobalAssemblyCache);

                if (verbose)
                {
                    Console.WriteLine("    Host context:   {0:x16}", targasm.HostContext);
                    Console.WriteLine("    MDS version:    {0:x8}", manmodule.MDStreamVersion);
                    Console.WriteLine("    Metadata token: {0:x8}", manmodule.MetadataToken);
                    Console.WriteLine("    MD module name: {0}", manmodule.Name);
                    Console.WriteLine("    MD module GUID: {0}", manmodule.ModuleVersionId);
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The assembly `{0}` does not seem to contain a valid .NET-header.", targnfo);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Use `rundll32.exe` for natice 32-Bit and 64-Bit MS/PE-assemblies or\n`rundll.exe` for native 16-Bit MS/PE-assemblies instead.");
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            #endregion
            #region REGEX FETCH SIGNATURE

            bool @static = args[1].ToLower().Trim() != "new";
            string method = args[1];
            
            if (!@static)
                if (args.Length < 3)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Missing method signature.{0}", helpstr);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                    method = args[2];

            method = method.Replace("::", ".").Trim();

            Match reg = REGEX_METHOD.Match(method);
            Signature sig = new Signature();

            if (reg.Success)
            {
                sig.Namespace = (reg.Groups["namespace"].ToString() ?? "").Trim('.', ' ', '\r', '\t', '\n');
                sig.Class = (reg.Groups["class"].ToString() ?? "").Trim('.', ' ', '\r', '\t', '\n');
                sig.Member = (reg.Groups["name"].ToString() ?? "").Trim('.', ' ', '\r', '\t', '\n');
                sig.Arguments = (from string s in (reg.Groups["parameters"].ToString() ?? "").TrimStart('(').TrimEnd(')').Split(',')
                                 select s.Trim()).ToArray();
                sig.IsProperty = !reg.Groups["parameters"].Success;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid method name format `{1}`.{0}", helpstr, method);
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            #endregion
            #region LOAD CLASS + MEMBER

            List<Assembly> asms = new List<Assembly>() {
                targasm,
                typeof(string).Assembly,
            };

            if ((from argv in args
                 let targv = argv.Trim().ToLower()
                 where targv == "--stdlib" ||
                       targv == "-s"
                 select false).Count() > 0)
            {
                asms.Add(typeof(Uri).Assembly);
                asms.Add(typeof(DataSet).Assembly);
                asms.Add(typeof(EnumerableQuery).Assembly);
            }

            if ((from argv in args
                 let targv = argv.Trim().ToLower()
                 where targv == "--uclib" ||
                       targv == "-u"
                 select false).Count() > 0)
                asms.Add(Assembly.LoadFrom("uclib.dll"));

            asms.AddRange(from string s in new string[] { /* TODO: ADD MORE ASSEMBLIES HERE */ } select Assembly.LoadFrom(s));

            IEnumerable<AssemblyName> asmnames = (from _ in asms select _.GetReferencedAssemblies().Union(new AssemblyName[] { _.GetName() }))
                                                 .SelectMany(_ => _).Distinct();

            alltypes = asmnames.SelectMany(_ => {
                try
                {
                    return Assembly.Load(_).GetTypes();
                }
                catch
                {
                    return new Type[0] { };
                }
            }).Distinct();

            Type @class = FetchType(sig.Namespace, sig.Class);

            if (@class == null)
                return 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Type `{0}` loaded from `{1}`.", @class.FullName, asmname.Name);
            Console.ForegroundColor = ConsoleColor.White;

            MethodInfo member = FetchMethod(@class, sig.Member, sig.IsProperty, sig.Arguments);

            if (member == null)
                return 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Member `{0}` loaded from `{1}`.", CommonLanguageRuntime.GetCPPSignature(member), asmname.Name);
            Console.ForegroundColor = ConsoleColor.White;

            #endregion
            #region PARSE ARGUMENTS

            List<object> parameters = new List<object>();

            args = args.Where(_ => !Regex.Match(_, @"(\/\?|\-\-?[a-z][0-9a-z\-_]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase).Success).ToArray();

            for (int i = 0, d = @static ? 2 : 3, l = args.Length - d; i < l; i++)
            {
                ParameterInfo pnfo = member.GetParameters()[i];
                string argv = args[i + d];
                string xml = "";
                object param;
                Match m;

                if ((m = Regex.Match(argv, @"^\@xml\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');
                        param = Serialization.Deserialize(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The given string does not seem to be a valid XML string.{0}", helpstr);
                        Console.ForegroundColor = ConsoleColor.White;

                        return 0;
                    }
                else if ((m = Regex.Match(argv, @"^\@xml\:\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');
                        xml = File.ReadAllText(xml);

                        try
                        {
                            param = Serialization.Deserialize(xml, pnfo.ParameterType);
                        }
                        catch
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The given string does not seem to be a valid XML string.{0}", helpstr);
                            Console.ForegroundColor = ConsoleColor.White;

                            return 0;
                        }
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The file `{0}` could not be found or accessed.", xml);
                        Console.ForegroundColor = ConsoleColor.White;

                        return 0;
                    }
                else if ((m = Regex.Match(argv, @"^\@json\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');

                        param = Serialization.DeserializeJSON(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The given string does not seem to be a valid JSON string.{0}", helpstr);
                        Console.ForegroundColor = ConsoleColor.White;

                        return 0;
                    }
                else if ((m = Regex.Match(argv, @"^\@json\:\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');

                        try
                        {
                            xml = File.ReadAllText(xml);
                        }
                        catch
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The file `{0}` could not be found or accessed.", xml);
                            Console.ForegroundColor = ConsoleColor.White;

                            return 0;
                        }

                        param = Serialization.DeserializeJSON(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The given string does not seem to be a valid JSON string.{0}", helpstr);
                        Console.ForegroundColor = ConsoleColor.White;

                        return 0;
                    }
                else
                    if (!ParseType(argv, pnfo.ParameterType, out param))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The given argument `{0}` could not be interpreted as `{1}`.", argv.Trim(), pnfo.ParameterType.GetCPPTypeString());
                        Console.ForegroundColor = ConsoleColor.White;

                        return 0;
                    }
                    else
                        parameters.Add(param);
            }

            #endregion
            #region INVOKE METHOD

            object instance, @return;
            object[] cparameters = new object[parameters.Count];
            ParameterInfo[] pnfos = member.GetParameters();

            for (int i = 0, l = cparameters.Length; i < l; i++)
                try
                {
                    cparameters[i] = Convert.ChangeType(parameters[i], pnfos[i].ParameterType);
                }
                catch (Exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The given argument `{0}` could not be cast as `{1}`.", parameters[i], pnfos[i].ParameterType.GetCPPTypeString());
                    Console.ForegroundColor = ConsoleColor.White;

                    return 0;
                }

            try
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Invoking member `{0}::{1}`...", asmname.Name, member.GetCPPSignature());
                Console.WriteLine("------------------------------------------------------------------------\n");
                Console.ForegroundColor = ConsoleColor.White;

                instance = @static ? null : Activator.CreateInstance(@class, true);
                @return = member.Invoke(instance, cparameters);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("-------------------------------- ERROR ---------------------------------");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An error occured:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[HResult 0x{2:x8}] {0}\n{1}\n{4}\n\nSee {3} for more information...", ex.Message, ex.StackTrace, ex.HResult, ex.HelpLink, ex.Data.ToDebugString());
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            #endregion
            #region PRINT RETURN VALUE

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("----------------------------- RETURN VALUE -----------------------------");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n{0}:", member.ReturnType.GetCPPTypeString());

            if (@return == null)
                Console.WriteLine("nullptr");
            else if (member.ReturnType.IsSerializable)
                try
                {
                    Console.WriteLine(new JavaScriptSerializer().Serialize(@return));
                }
                catch
                {
                    Console.WriteLine(@return.var_dump(depth:3));
                }
            else
                Console.WriteLine(@return);

            #endregion
            #region
            #endregion

            return 0;
        }

        private static bool ParseType(string argv, Type type, out dynamic param)
        {
            bool isnum = false;
            bool isflt = false;
            Match m;

            try
            {
                if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(0x(?<value>[0-9a-f]+)h?|(0x)?(?<value>[0-9a-f]+)h)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    ulong ul = ulong.Parse(m.Groups["value"].ToString(), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);

                    argv = (m.Groups["sign"].ToString().Contains('-') ? "-" : "") + ul;
                    isnum = true;
                }
                else if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(?<value>[0-9]*(\.|\,)?[0-9]+([e][\-\+]?[0-9]+)?)(d|f|m)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    decimal dec = decimal.Parse(m.Groups["value"].ToString().Replace('.', ','));

                    argv = (m.Groups["sign"].ToString().Contains('-') ? "-" : "") + dec;

                    isflt = true;
                }

                param = new TypeMatcher<object>()
                    .Case(T => argv.ToLower().Trim() == "null", _ => null)
                    .Case<string>(argv)
                    .Case<char>(_ => {
                        if (argv.Length == 1)
                            return argv[0];

                        m = Regex.Match(argv, @"^\s*\'(?<value>(\\(b|r|n|t|a|f|v|0|\""|\'|\\|x[0-9a-f]{1,2}|u[0-9a-f]{1,4}))|[\u0000-\uffff])\'\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        return char.Parse(m.Groups["value"].ToString());
                    })
                    .Case(LINQExtensions.GetTypes<float, double, decimal>(), (_, T) => decimal.Parse(argv))
                    .Case(LINQExtensions.GetTypes<sbyte, byte, short, ushort, int, uint, long, ulong>(), (_, T) => argv.Contains('-') ? (object)long.Parse(argv) : (object)ulong.Parse(argv))
                    .Case<bool>((_, T) => isnum ? long.Parse(argv) != 0L : isflt ? decimal.Parse(argv) != 0m : bool.Parse(argv))
                    .Case(T => T.IsArray, (_, T) => {
                        Type t = T.GetElementType();

                        m = Regex.Match(argv, @"^\s*\{\s*(?<values>.*(?:\s*,\s*.*\s*)*)?\s*\}\s*$", RegexOptions.Compiled);

                        if (m.Groups["values"].Success)
                        {
                            argv = m.Groups["values"].ToString().Trim();

                            List<object> obj = new List<object>();

                            foreach (string s in from elem in argv.Split(',')
                                                 let telem = elem.Trim()
                                                 where telem.Length > 0
                                                 select telem)
                            {
                                object elem;

                                if (ParseType(s, t, out elem))
                                    obj.Add(elem);
                                else
                                    throw null;
                            }

                            return obj.ToArray();
                        }
                        else
                            return Array.CreateInstance(t, 0);
                    })
                    .Case(T => T.IsPointer, (_, T) => {
                        object v = null;

                        if ((argv = argv.Trim()).StartsWith("&"))
                        {
                            ParseType(argv.Remove(0, 1), typeof(long), out v);

                            return Pointer.Box((long*)(long)v, T);
                        }
                        else
                        {
                            Type t = T.GetElementType();

                            ParseType(argv, t, out v);


                            // TODO : CREATE POINTER


                            return null;
                        }
                    })
                    // ANY OTHER CASES
                    [type, null];

                return true;
            }
            catch
            {
                param = null;

                return false;
            }
        }

        public static bool CheckHelp(string[] args)
        {
            if (args.Length == 0 || (from arg in args
                                     let targ = arg.ToLower().Trim()
                                     where targ == "--help" ||
                                           targ == "-h" ||
                                           targ == "/?" ||
                                           targ == "-?" 
                                     select true).Count() > 0)
            {
                string helpstr = string.Format(Properties.Resources.PrintedHeader.Replace("{0}", "{1}") + @"
This application allows the execution of static and instance functions inside
compiled .NET-assemblies, e.g. inside static and dynamic libraries or controls
(aka .dll, .module, .tlb, .olb, .ocx, .exe, ...). All method I/O-stream output
will be redirected to the current console host (if not otherwise instructed
inside the called method).

The usage is defined as follows:
    {0} <library> ['new'] <method> [arguments, ...]

with the following parameters:
    libaray   - The .NET assembly file name or path
    'new'     - The 'new'-keyword is optional, but must be passed if the
                method in question is not a static one (without the quotes).
    method    - Either a fully qualified namespace, class and method signature
                or the name of the class (if unique) followed by the unique
                method name (or optinal parameters to identify the method).
    arguments - An optional list of arguments (separated by commas without any
                whitespace), which will be passed as method parameters to the
                given method. A serilaized XML- or JSON-string can be passed
                with @XML:""...."" or @JSON:""...."", where .... is the XML-
                or JSON-string in question. If the parameter shall bede seria-
                lized from a JSON- or XML-file, the argument shall be passed
                as @JSON::""...."" or @XML::""...."", where .... represents
                the path to the given JSON- or XML-file.

The following options are also defined:
    -v, --verbose - Prints verbose information about the loaded assembly.
    -s, --stdlib  - Includes the .NET standard libraries (`System.Data`
                    `System` and `System.Core`).
                    Note: The library `mscorlib` is always included.
    -u, --uclib   - Includes the .NET library `uclib`
    -h, --help    - Displays this help page.
                
Valid usage examples are:
    {0} mscorlib.dll System.IntPtr.Size
    {0} /root/Documents/library.olb new ImgLib.Image.Rotate()
    {0} \\127.0.0.1\app.exe new MainWindow::.ctor(string) ""foobar"" --stdlib
".TrimEnd(), modname, nfo.Name);
                string[] lines = helpstr.Split('\n');

                if (Console.WindowHeight < lines.Length + 1)
                    Console.WindowHeight = lines.Length + 2;
                if (Console.WindowWidth < lines.Max(x => x.Length + 1))
                    Console.WindowWidth = lines.Max(x => x.Length + 2);

                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleExtensions.AdvancedAnimatedWriteLine(helpstr, 2f);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;

                return true;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(Properties.Resources.PrintedHeader, nfo.Name);
            Console.WriteLine("Arguments: {0}", string.Join(" ", args));

            return false;
        }

        public static Type FetchType(string @namespace, string @class)
        {
            if (string.IsNullOrWhiteSpace(@namespace))
                if (PRIMITIVES.ContainsKey(@class))
                {
                    Tuple<string, string> name = PRIMITIVES[@class];

                    @namespace = name.Item1;
                    @class = name.Item2;
                }
                else if (@class.Contains('.'))
                {
                    @namespace = @class.Remove(@class.LastIndexOf('.'));
                    @class = @class.Remove(0, @class.LastIndexOf('.') + 1);
                }

            string __fclassnamenrm = @namespace + (@namespace.Length > 0 ? "." : "") + @class;
            string __fclassnamelwr = __fclassnamenrm.ToLower();
            IEnumerable<Type> classes = from Type t in alltypes
                                        where t.FullName.ToLower().EndsWith(__fclassnamelwr)
                                        select t;

            if (classes.Count() == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm, asmname.Name, asmname.Version);
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (classes.Count() > 1)
            {
                IEnumerable<Type> oclasses = classes;

                classes = from Type t in alltypes
                          where t.Name == @class
                          where t.Namespace == @namespace
                          select t;

                if (classes.Count() == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm, asmname.Name, asmname.Version);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("However, the following types have been found, which partly match the given type name:");

                    foreach (Type t in oclasses)
                        Console.WriteLine("    {0}", t.FullName);

                    Console.ForegroundColor = ConsoleColor.White;
                }
                else if (classes.Count() > 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The type `{0}` could not be determined, as it is not unique:");

                    foreach (Type t in classes)
                        Console.WriteLine("    [{1}]{0}", t.FullName, t.Assembly.CodeBase);

                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                    return classes.First();
            }
            else
                return classes.First();

            return null;
        }

        public static MethodInfo FetchMethod(Type tp, string name, bool isprop, params string[] parameters)
        {
            MethodInfo[] members = tp.GetMethods((BindingFlags)0x01063f7f);
            IEnumerable<MethodInfo> match = from m in members
                                            where m.Name == name
                                            select m;
            List<Tuple<Type, ParameterAttributes>> paramtypes = new List<Tuple<Type, ParameterAttributes>>();

            if (match.Count() > 0)
                if (match.Count() == 1)
                    return match.First();
                else
                {
                    if (!isprop)
                        foreach (string p in parameters)
                        {
                            ParameterAttributes pa = default(ParameterAttributes);
                            string _p = p;

                            Match m = Regex.Match(p, @"^(\&\s*|ref\s+)", RegexOptions.IgnoreCase);

                            if (m.Success)
                            {
                                pa |= ParameterAttributes.In | ParameterAttributes.Out;
                                _p = p.Remove(m.Index, m.Length);
                            }
                            else if ((m = Regex.Match(p, @"^out\s+", RegexOptions.IgnoreCase)).Success)
                            {
                                pa |= ParameterAttributes.Out;
                                _p = p.Remove(m.Index, m.Length);
                            }
                            else if ((m = Regex.Match(p, @"^in\s+", RegexOptions.IgnoreCase)).Success)
                            {
                                pa |= ParameterAttributes.In;
                                _p = p.Remove(m.Index, m.Length);
                            }

                            if ((m = Regex.Match(_p, @"(\s*\[\s*\,*\s*\]\s*)+$")).Success)
                            {
                                _p = _p.Remove(m.Index, m.Length);

                                string brackets = m.ToString().Replace(" ", "");


                                // TODO : ANALYZE BRACKETS AND CREATE ARRAY TYPE
                            }
                            if ((m = Regex.Match(_p, @"(\s*\**\s*)+$")).Success)
                            {
                                _p = _p.Remove(m.Index, m.Length);

                                int ptrdepth = m.ToString().Replace(" ", "").Length;


                                // TODO : ANALYZE POINTER DEPTH AND CREATE POINTER TYPE
                            }
                        
                            Type t = FetchType("", _p);

                            // var @ref = new CodeTypeReference("System.Collections.Generic.IEnumerable<System.Reflection.MethodInfo>[][][]");


                            if (t == null)
                                return null;
                            else
                                paramtypes.Add(new Tuple<Type, ParameterAttributes>(t, pa));
                        }

                     match = isprop ? from m in match
                                      where true // TODO : PROPERTIES
                                      select m
                                    : from m in match
                                      let margs = from p in m.GetParameters()
                                                  select new Tuple<Type, ParameterAttributes>(p.ParameterType, p.Attributes & (ParameterAttributes.In | ParameterAttributes.Out))
                                      where new Func<bool>(() => {
                                          var ma = margs.ToArray();
                                          var pa = paramtypes.ToArray();

                                          if (ma.Length != pa.Length)
                                              return false;

                                          for (int i = 0; i < ma.Length; i++)
                                              if (ma[i].Item2 != pa[i].Item2)
                                                  return false;
                                              else if (ma[i].Item1.FullName != pa[i].Item1.FullName)
                                                  return false;

                                          return true;
                                      })()
                                      select m;

                    if (match.Count() == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The member `{0}` could not be found inside the type `{1}::{2}`.", name, asmname.Name, tp.FullName);
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (match.Count() > 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The member `{0}` does match mulitple members inside `{1}::{2}`:", name, asmname.Name, tp.FullName);
                        Console.ForegroundColor = ConsoleColor.Yellow;

                        foreach (MethodInfo mnfo in match)
                            Console.WriteLine("    [{1}]  {0}", mnfo.GetCPPSignature(), asmname.Name);

                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                        return match.First();
                }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The member `{0}` could not be found inside the type `{1}::{2}`.", name, asmname.Name, tp.FullName);
                Console.ForegroundColor = ConsoleColor.White;
            }

            return null;
        }
    }

    public class Signature
    {
        public string Class { get; set; }
        public string Member { get; set; }
        public bool IsProperty { get; set; }
        public string Namespace { get; set; }
        public string[] Arguments { get; set; }

        public string FullClass
        {
            get
            {
                return Namespace + '.' + Class;
            }
        }

        public string FullMember
        {
            get
            {
                return FullClass + '.' + Member;
            }
        }
    }
}
