using System.Windows.Forms.Integration;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Diagnostics;
using System.Collections;
using CoreLib.Management;
using System.Reflection;
using Microsoft.CSharp;
using System.Windows;
using System.CodeDom;
using System.Data;
using System.Linq;
using System.Text;
using System.Xaml;
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
        internal const string STR_REGEX_PARAMBASE = @"\s*(\s*\,?\s*((ref|out|in)\s+|\&\s*)?(\w+\.)*\w+(\s*((\[(\,)*\])+|\*+))?)+\s*";
        internal static readonly Regex REGEX_TYPE = new Regex(@"(?<namespace>(\w+\.)*)(?<class>\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static readonly Regex REGEX_METHOD = new Regex(@"((?<namespace>(\w+\.)*)(?<class>\w+\.))?((?<name>([a-z_]\w*|\/\/(c?c|d)tor))(?<parameters>\(" + STR_REGEX_PARAMBASE + @"\))?|(?<name>this)(?<parameters>\[" + STR_REGEX_PARAMBASE + @"\])?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
            //AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((o, a) => {
            //    if (a.Name.ToLower().Contains("uclib"))
            //        return Assembly.Load(Properties.Resources.uclib);
            //    else
            //        return null;
            //});
        }
        
        /// <summary>
        /// The application's entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        public static int Main(string[] args)
        {
            try
            {
                typeof(Assembly).GetMethod("GetReferencedAssemblies").WarmUp();

                foreach (MethodInfo nfo in typeof(IEnumerable<>).GetMethods())
                    if (!nfo.IsAbstract)
                        nfo.WarmUp();

                int retcode = InnerMain(args);

                if (Debugger.IsAttached)
                    Win32.system("pause");

                return retcode;
            }
            catch (Exception ex)
            {
                return _err("An internal error occured:\n\t{0}\n{1}\n{2}\nHRESULT 0x{3:x8}\nSee `{4}` for more information...", ex.Message, ex.StackTrace, ex.Data.ToDebugString(), ex.HResult, ex.HelpLink) + 1;
            }
        }

        /// <summary>
        /// The application's main method
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        public static int InnerMain(string[] args)
        {
            #region ARG CHECK, ASM LOADING

            if (DisplayHelp(args))
                return 0;
            else if (args.RegArguments().Length < 2)
            {
                _err("Invalid argument count.{0}", helpstr);

                return 0;
            }

            targnfo = new FileInfo(args[0]);

            if (!targnfo.Exists)
                return _err("The file `{0}` could not not be found.", targnfo);
            if (LoadMainAssembly(args) == 0)
                return 0;

            #endregion
            #region REGEX FETCH SIGNATURE

            bool @static = args[1].ToLower().Trim() != "new";
            string method = args[1];
            
            if (!@static)
                if (args.Length < 3)
                    return _err("Missing method signature.{0}", helpstr);
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
                return _err("Invalid method name format `{1}`.{0}", helpstr, method);

            #endregion
            #region LOAD CLASS + MEMBER

            LoadAssemblies(args);

            Type @class = FetchType(sig.Namespace, sig.Class);

            if (@class == null)
                return 0;

            _ok("Type `{0}` loaded from `{1}`.", @class.FullName, asmname.Name);

            bool constructor;
            object result = FetchMethod(@class, sig.Member, sig.IsProperty, ref @static, out constructor, sig.Arguments);
            ConstructorInfo cmember = null;
            MethodInfo member = null;

            if (result == null)
                return 0;
            else if (!constructor)
                member = result as MethodInfo;
            else
                cmember = result as ConstructorInfo;

            _ok("Member `{0}` loaded from `{1}`.", MemberName(result, constructor), asmname.Name);

            #endregion
            #region INVOKE METHOD

            List<object> parameters = new List<object>();

            if (ParseParameters(args, @static, constructor, cmember, member, out parameters) == 0)
                return 0;

            object instance, @return;
            object[] cparameters = new object[parameters.Count];
            ParameterInfo[] pnfos = (constructor ? cmember.GetParameters() : member.GetParameters());

            if (cparameters.Length < pnfos.Length)
                return _err("Parameter count mismatch: The method `{0}` requires {1} parameter(s), but only {2} were given.", MemberName(result, constructor), pnfos.Length, cparameters.Length);

            for (int i = 0, l = pnfos.Length; i < l; i++)
                try
                {
                    cparameters[i] = Convert.ChangeType(parameters[i], pnfos[i].ParameterType);
                }
                catch
                {
                    return _err("The given argument `{0}` could not be cast as `{1}`.", parameters[i], pnfos[i].ParameterType.GetCPPTypeString());
                }

            try
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Invoking member `{0}::{1}`...", asmname.Name, MemberName(result, constructor));
                Console.WriteLine("--------------------------- STDSTREAM OUTPUT ---------------------------\n");
                Console.ForegroundColor = ConsoleColor.White;

                if (constructor)
                    @return = cmember.Invoke(cparameters);
                else
                {
                    instance = @static ? null : Activator.CreateInstance(@class, true);

                    @return = member.Invoke(instance, cparameters);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("\n-------------------------------- ERROR ---------------------------------");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An error occured:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[HResult 0x{2:x8}] {0}\n{1}\n{4}\n\nSee `{3}` for more information...", ex.Message, ex.StackTrace, ex.HResult, ex.HelpLink, ex.Data.ToDebugString());
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            PrintReturnValue(args, constructor, @static, @return, @class, member);

            #endregion
            #region GC COLLECTION

            try
            {
                if (@return is GCHandle)
                    ((GCHandle)@return).Free();
            } catch { }

            try
            {
                if (@return is IDisposable)
                    (@return as IDisposable).Dispose();
            } catch { }

            @return = null;

            GC.Collect();

            #endregion

            return 0;
        }
        
        /// <summary>
        /// Prints the given return value
        /// </summary>
        /// <param name="args">Command line arguments?</param>
        /// <param name="constructor">Is constructor?</param>
        /// <param name="static">Is the method static?</param>
        /// <param name="return">Method return value</param>
        /// <param name="class">Parent type</param>
        /// <param name="member">Member information</param>
        public static void PrintReturnValue(string[] args, bool constructor, bool @static, object @return, Type @class, MethodInfo member)
        {
            if (!(constructor && @static))
            {
                Console.ForegroundColor = ConsoleColor.Gray;

                if (constructor)
                    Console.WriteLine("\n---------------------------- OBJECT INSTANCE ---------------------------");
                else
                    Console.WriteLine("\n----------------------------- RETURN VALUE -----------------------------");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n{0}:", (constructor ? @class : member.ReturnType).GetCPPTypeString());

                if (@return == null)
                    Console.WriteLine("nullptr");
                else if ((constructor ? @class : member.ReturnType).IsSerializable)
                    try
                    {
                        Console.WriteLine(Serialization.FormatJSON(new JavaScriptSerializer().Serialize(@return)));
                    }
                    catch
                    {
                        int depth = 1;

                        try
                        {
                            depth = (from argv in args
                                     let targv = argv.Trim().ToLower()
                                     where targv.StartsWith("--depth") ||
                                           targv.StartsWith("-d")
                                     select int.Parse(Regex.Match(targv, @"^\s*(\-\-depth|\-d)(?<value>[1-7])\s*$").Groups["value"].ToString())).Max();
                        }
                        catch { }

                        Console.WriteLine(@return.var_dump(CheckPrintability("├─└╞═╘"/* <--ibm850 */), depth)); //  "├─└╞═╘" /* <--unicode */
                    }
                else
                    Console.WriteLine(@return);
            }

            Console.WriteLine("\n------------------------------------------------------------------------\n");
        }

        /// <summary>
        /// Checks, wether the given string can be printed inside the stdio-stream using the current character encoding
        /// </summary>
        /// <param name="p">String to be printed</param>
        /// <returns>Check result</returns>
        public static bool CheckPrintability(string p)
        {
            return CheckPrintability(p, Console.OutputEncoding);
        }

        /// <summary>
        /// Checks, wether the given string can be printed using the given character encoding
        /// </summary>
        /// <param name="p">String to be printed</param>
        /// <param name="enc">Character encoding</param>
        /// <returns>Check result</returns>
        public static bool CheckPrintability(string p, Encoding enc)
        {
            return enc.GetString(enc.GetBytes(p)) == p;
        }

        /// <summary>
        /// Displays the help page on demand
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Return value, which indicates, whether the help page has been shown</returns>
        public static bool DisplayHelp(string[] args)
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
                Use `.//ctor`, `.//cctor`, `.//dtor` after the parent type to
                address the type's instance  constructor, static constructor
                or destructor.
    arguments - An optional list of arguments (separated by commas without any
                whitespace), which will be passed as method parameters to the
                given method. A serilaized XML- or JSON-string can be passed
                with @XML:""...."" or @JSON:""...."", where .... is the XML-
                or JSON-string in question. If the parameter shall bede seria-
                lized from a JSON- or XML-file, the argument shall be passed
                as @JSON::""...."" or @XML::""...."", where .... represents
                the path to the given JSON- or XML-file.

The following options are also defined:
    -d_, --depth_  - Sets the return value print depth (the symbol `_` must be
                     a positive integer value between 1 and 7).
    -v, --verbose  - Prints verbose information about the loaded assembly.
    -s, --stdlib   - Includes the .NET standard libraries (`System.Data`
                     `System` and `System.Core`).
                     Note: The library `mscorlib` is always included.
    -w, --wpflib   - Includes the .NET WPF (Windows Presentation Framework)
                     libraries (`System.Xaml.dll``PresentationCore.dll`,
                     `WindowsBase.dll`, `PresentationFramework.dll` and
                     `WindowsFormsIntegration.dll`).
    -u, --uclib    - Includes the .NET Unknown6656 core library `uclib`
    -e:, --extlib: - Includes the given .NET library and loads its types. The
                     assembly's file path must be given directly after the
                     colon (`:`). This option can be given multiple times.
    -h, --help     - Displays this help page.
                
Valid usage examples are:
    {0} mscorlib.dll System.IntPtr.Size --depth2
    {0} /root/Documents/library.olb new ImgLib::Image::Rotate()
    {0} \\127.0.0.1\app.exe new MainWindow.//ctor(string) ""foobar"" --stdlib
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

        /// <summary>
        /// Checks the given argument enumeration for the given short and long option
        /// </summary>
        /// <param name="args">Argument enumeration</param>
        /// <param name="long">Long argument (without `--`)</param>
        /// <param name="short">Short argument (without `-`)</param>
        /// <returns>Check result</returns>
        public static bool CheckForOption(IEnumerable<string> args, string @long, string @short)
        {
            @long = "--" + @long.ToLower();
            @short = "-" + @short.ToLower();

            return (from argv in args
                    let targv = argv.Trim().ToLower()
                    where targv == @long ||
                          targv == @short
                    select false).Count() > 0;
        }

        /// <summary>
        /// Ignores all option arguments and returns the non-option ones
        /// </summary>
        /// <param name="args">Argument enumeration</param>
        /// <returns>Non-option arguments</returns>
        public static string[] RegArguments(this IEnumerable<string> args)
        {
            return (from a in args
                    let ta = a.Trim()
                    where !Regex.Match(ta, @"^(\/\?|\-\-?[a-z][0-9a-z\-_]*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase).Success
                    select a).ToArray();
        }

        /// <summary>
        /// Loads the main assembly and displays basic and/or advanced information (based on the given command line arguments)
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Return code</returns>
        public static int LoadMainAssembly(string[] args)
        {
            try
            {
                PEHeader hdr;

                try
                {
                    hdr = new PEHeader(targnfo.FullName);
                }
                catch
                {
                    return _err("The file `{0}` does not seem to contain a valid PE/COFF header.", targnfo);
                }

                targasm = Assembly.LoadFrom(targnfo.FullName);
                asmname = targasm.GetName();

                Module manmodule = targasm.ManifestModule;

                bool verbose = CheckForOption(args, "verbose", "v");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Assembly loaded:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("    Name:           {0}", asmname.Name);
                Console.WriteLine("    Version:        {0}", asmname.Version);
                Console.WriteLine("    Full name:      {0}", asmname.FullName);

                if (verbose)
                    Console.WriteLine("    Public key:     {0}", string.Join("", from b in asmname.GetPublicKey() ?? new byte[0] select b.ToString("X2")));

                Console.WriteLine("    Architecture:   {0}, {1}Bit", asmname.ProcessorArchitecture, hdr.Is32BitHeader ? 32 : 64);

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
                    Console.WriteLine("    PE DLL Charac.: 0x{0:x4} ({1})", hdr.FileHeader.Characteristics, (PEDLLCharacteristics)hdr.FileHeader.Characteristics);
                    Console.WriteLine("    Machine:        0x{0:x4} ({1})", hdr.FileHeader.Machine, (IMAGE_FILE_MACHINE)hdr.FileHeader.Machine);

                    IMAGE_DATA_DIRECTORY clrhdr;
                    int subs = hdr.Is32BitHeader ? hdr.OptionalHeader32.Subsystem : hdr.OptionalHeader64.Subsystem;
                    int chrs = hdr.Is32BitHeader ? hdr.OptionalHeader32.DllCharacteristics : hdr.OptionalHeader32.DllCharacteristics;

                    Console.WriteLine("    Subsystem:      0x{0:x4} ({1})", subs, PEHeader.SubsystemStrings[(IMAGE_SUBSYSTEM)subs]);

                    if (hdr.Is32BitHeader)
                    {
                        Console.WriteLine("    Size of code:   0x{0:x8}", hdr.OptionalHeader32.SizeOfCode);
                        Console.WriteLine("    Base of code:   0x{0:x8}", hdr.OptionalHeader32.BaseOfCode);
                        Console.WriteLine("    Base of data:   0x{0:x8}", hdr.OptionalHeader32.BaseOfData);
                        Console.WriteLine("    Entrypt. addr.: 0x{0:x8}", hdr.OptionalHeader32.AddressOfEntryPoint);

                        clrhdr = hdr.OptionalHeader32.CLRRuntimeHeader;
                    }
                    else
                    {
                        Console.WriteLine("    Size of code:   0x{0:x8}", hdr.OptionalHeader64.SizeOfCode);
                        Console.WriteLine("    Base of code:   0x{0:x8}", hdr.OptionalHeader64.BaseOfCode);
                        Console.WriteLine("    Entrypt. addr.: 0x{0:x8}", hdr.OptionalHeader64.AddressOfEntryPoint);

                        clrhdr = hdr.OptionalHeader64.CLRRuntimeHeader;
                    }

                    Console.WriteLine("    Opt. DLL Char.: 0x{0:x4} ({1})", chrs, (OptionalDLLCharacteristics)chrs);
                    Console.WriteLine("    CLR-Hdr. size:  0x{0:x8}", clrhdr.Size);
                    Console.WriteLine("    CLR virt.addr.: 0x{0:x8}", clrhdr.VirtualAddress);
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The PE-file `{0}` does not seem to contain a valid .NET-header.", targnfo);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Use `rundll32.exe` for natice 32-Bit and 64-Bit MS/PE-assemblies or\n`rundll.exe` for native 16-Bit MS/PE-assemblies instead.");
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            return 1;
        }

        /// <summary>
        /// Loads all assembly types based on the given arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void LoadAssemblies(string[] args)
        {
            List<Assembly> asms = new List<Assembly>() {
                targasm,
                typeof(string).Assembly,
            };

            if (CheckForOption(args, "stdlib", "s"))
            {
                asms.Add(typeof(Uri).Assembly);
                asms.Add(typeof(DataSet).Assembly);
                asms.Add(typeof(EnumerableQuery).Assembly);
            }

            if (CheckForOption(args, "wpflib", "w"))
            {
                asms.Add(typeof(XamlDirective).Assembly);
                asms.Add(typeof(WindowsFormsHost).Assembly);
                asms.Add(typeof(DependencyObject).Assembly);
                asms.Add(typeof(System.Windows.DataObject).Assembly);
                asms.Add(typeof(ConditionCollection).Assembly);
            }

            // TODO : winformlib
            // TODO : fsharplib
            // TODO : wcflib
            // TODO : system.security, webext, encryption, ...

            if (CheckForOption(args, "uclib", "u"))
                asms.Add(Assembly.LoadFrom("uclib.dll"));

            asms.AddRange(from string argv in args
                          let targv = argv.ToLower().Trim()
                          where targv.StartsWith("--extlib:") || targv.StartsWith("-e:")
                          let tasm = new Func<Assembly>(delegate {
                              string loc = targv.Remove(0, targv.IndexOf(':') + 1).Trim();

                              try
                              {
                                  return Assembly.LoadFrom(loc);
                              }
                              catch
                              {
                                  _err("The external assembly `{0}` could not be loaded and has therefore been ignored.", loc);

                                  return null;
                              }
                          }).Invoke()
                          where tasm != null
                          select tasm);
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
        }

        /// <summary>
        /// Fetches a type based on the namespace and typename
        /// </summary>
        /// <param name="namespace">Namespace name</param>
        /// <param name="class">Type name</param>
        /// <returns>Type</returns>
        public static Type FetchType(string @namespace, string @class)
        {
            if ((@namespace.Trim().Length + @class.Trim().Length) == 0)
                return _err("The empty string is not a valid type name.").NULL() as Type;

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
                _err("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm, asmname.Name, asmname.Version);
            else if (classes.Count() > 1)
            {
                IEnumerable<Type> oclasses = classes;

                classes = from Type t in alltypes
                          where t.Name == @class
                          where t.Namespace == @namespace
                          select t;

                if (classes.Count() == 0)
                {
                    _err("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm, asmname.Name, asmname.Version);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("However, the following types have been found, which partly match the given type name:");

                    int oclen = oclasses.Count();
                    const int ocmaxlen = 24;

                    foreach (Type t in oclasses.Take(ocmaxlen))
                        Console.WriteLine("    {0}", t.FullName);

                    if (oclen > ocmaxlen)
                    {
                        oclen -= ocmaxlen;

                        Console.WriteLine("{0} more entries follow. Do you want to show them? (y/N)", oclen);

                        if (char.ToLower(Console.ReadKey(false).KeyChar) == 'y')
                            foreach (Type t in oclasses.Skip(ocmaxlen))
                                Console.WriteLine("    {0}", t.FullName);
                    }

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

        /// <summary>
        /// Parses the given parameters based on the given command line arguments and returns the parameter object instance list
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="static">Is the method static?</param>
        /// <param name="constructor">Is the method a constructor?</param>
        /// <param name="cmember">Constructor information</param>
        /// <param name="member">Method information</param>
        /// <param name="parameters">Parameter object instance list</param>
        /// <returns>Return code</returns>
        public static int ParseParameters(string[] args, bool @static, bool constructor, ConstructorInfo cmember, MethodInfo member, out List<object> parameters)
        {
            parameters = new List<object>();

            args = args.RegArguments();

            for (int i = 0, d = @static ? 2 : 3, l = args.Length - d; i < l; i++)
            {
                ParameterInfo pnfo = (constructor ? cmember.GetParameters() : member.GetParameters())[i];
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
                        return _err("The given string does not seem to be a valid XML string.{0}", helpstr);
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
                            return _err("The given string does not seem to be a valid XML string.{0}", helpstr);
                        }
                    }
                    catch
                    {
                        return _err("The file `{0}` could not be found or accessed.", xml);
                    }
                else if ((m = Regex.Match(argv, @"^\@json\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');

                        param = Serialization.DeserializeJSON(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        return _err("The given string does not seem to be a valid JSON string.{0}", helpstr);
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
                            return _err("The file `{0}` could not be found or accessed.", xml);
                        }

                        param = Serialization.DeserializeJSON(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        return _err("The given string does not seem to be a valid JSON string.{0}", helpstr);
                    }
                else
                    if (!ParseParamter(argv, pnfo.ParameterType, out param))
                        return _err("The given argument `{0}` could not be interpreted as `{1}`.", argv.Trim(), pnfo.ParameterType.GetCPPTypeString());
                    else
                        parameters.Add(param);
            }

            return 1;
        }

        /// <summary>
        /// Parses the given parameter based on the given type and returns the parsing result
        /// </summary>
        /// <param name="argv">Parameter representation</param>
        /// <param name="type">Parameter type</param>
        /// <param name="param">Parsed parameter value</param>
        /// <returns>Indicates, whether the parsing was successfull</returns>
        public static bool ParseParamter(string argv, Type type, out dynamic param)
        {
            bool isnum = false;
            bool isflt = false;
            Match m;

            try
            {
                if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(0x(?<value>[0-9a-f]+)h?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    ulong ul = ulong.Parse(m.Groups["value"].ToString(), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);

                    argv = (m.Groups["sign"].ToString().Contains('-') ? "-" : "") + ul;
                    isnum = true;
                }
                else if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(0o(?<value>[0-7]+))\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    ulong ul = (ulong)Convert.ToInt64(m.Groups["value"].ToString(), 8);

                    argv = (m.Groups["sign"].ToString().Contains('-') ? "-" : "") + ul;
                    isnum = true;
                }
                else if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(0b(?<value>[01]+))\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    ulong ul = (ulong)Convert.ToInt64(m.Groups["value"].ToString(), 2);

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

                                if (ParseParamter(s, t, out elem))
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
                            ParseParamter(argv.Remove(0, 1), typeof(long), out v);

                            return Pointer.Box((long*)(long)v, T);
                        }
                        else
                        {
                            Type t = T.GetElementType();

                            ParseParamter(argv, t, out v);

                            if (t.IsValueType)
                            {

                                // TODO : CREATE POINTER


                            }
                            else
                                v = GCHandle.Alloc(v);

                            return null;
                        }
                    })
                    .Case<Type>(_ => {
                        m = REGEX_TYPE.Match(argv);

                        return FetchType(m.Groups["namespace"].ToString(), m.Groups["class"].ToString());
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

        /// <summary>
        /// Fetches the parameter type and attributes from its given string representation
        /// </summary>
        /// <param name="p">Parameter string representation</param>
        /// <returns>Parameter type and attributes</returns>
        public static Tuple<Type, ParameterAttributes> FetchParameter(string p)
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

            if ((m = Regex.Match(_p, @"(\s*\[\s*(\,*\s*)*\]\s*)+$")).Success)
            {
                _p = _p.Remove(m.Index, m.Length).Trim();

                string brackets = m.ToString().Replace(" ", "");

                Tuple<Type, ParameterAttributes> tpl = FetchParameter(_p);

                if (tpl != null)
                    return null;
                else
                    return new Tuple<Type, ParameterAttributes>(ParseArray(tpl.Item1, brackets), tpl.Item2);
            }
            if ((m = Regex.Match(_p, @"(\s*\**\s*)+$")).Success)
            {
                _p = _p.Remove(m.Index, m.Length);

                int ptrdepth = m.ToString().Replace(" ", "").Length;



                // TODO : ANALYZE POINTER DEPTH AND CREATE POINTER TYPE
            }

            // var @ref = new CodeTypeReference("System.Collections.Generic.IEnumerable<System.Reflection.MethodInfo>[][][]");

            Type t = FetchType("", _p);

            if (t == null)
                return null;
            else
                return new Tuple<Type, ParameterAttributes>(t, pa);
        }

        /// <summary>
        /// Returns the formatted member name based on the given MethodBase-subclass
        /// </summary>
        /// <param name="member">MethodBase-subclass instance</param>
        /// <param name="constructor">Is the given member a constructor?</param>
        /// <returns>Formatted member name</returns>
        public static string MemberName(object member, bool constructor)
        {
            if (constructor)
                return (member as MethodBase).DeclaringType.GetCPPTypeString() + ((member as MethodBase).IsStatic ? "::.cctor(unit)" : "::.ctor(unit)");
            else
                return (member as MethodInfo).GetCPPSignature();
        }

        /// <summary>
        /// Fetches the method (not constructor!) which matches the given criteria
        /// </summary>
        /// <param name="tp">Declaring type</param>
        /// <param name="name">Method name</param>
        /// <param name="isprop">Is it a property?</param>
        /// <param name="parameters">Method parameter type list</param>
        /// <returns>MethodInfo-instance</returns>
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
                    Tuple<Type, ParameterAttributes> res;

                    if (!isprop)
                        foreach (string p in parameters)
                            if ((res = FetchParameter(p)) == null)
                                return null;
                            else
                                paramtypes.Add(res);

                    PropertyInfo[] props = tp.GetProperties();

                    match = isprop ? from m in match
                                     where m.IsSpecialName
                                     let sel = from p in props
                                               where p.GetSetMethod() == m ||
                                                     p.GetGetMethod() == m ||
                                                     p.Name == name
                                               select p
                                     where sel.Count() > 1
                                     where new Func<bool>(delegate {
                                         return true; // TODO: Fix on property matching
                                     })()
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

        /// <summary>
        /// Fetches the method which matches the given criteria
        /// </summary>
        /// <param name="tp">Declaring type</param>
        /// <param name="name">Method name</param>
        /// <param name="isprop">Is it a property?</param>
        /// <param name="static">Is it static?</param>
        /// <param name="constructor">Returns, whether the method is a constructor</param>
        /// <param name="parameters">Method parameter type list</param>
        /// <returns>MethodBase/MethodInfo/ConstructorInfo-instance</returns>
        public static dynamic FetchMethod(Type tp, string name, bool isprop, ref bool @static, out bool constructor, params string[] parameters)
        {
#pragma warning disable 183, 184
            constructor = false;

            if (Regex.Match(name, @"^\/\/\w+$").Success)
            {
                name = name.Remove(0, 2).ToLower();

                ConstructorInfo[] nfos;

                if ((name == "ctor"))
                    nfos = tp.GetConstructors((BindingFlags)0x01063f7f);
                else if (name == "dtor")
                {
                    MethodInfo nfo = tp.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                    if (@static)
                        @static = _war("The keyword `new` is missing.") is string; // constant false

                    if (nfo == null)
                        _err("The type `{1}::{0}` has no destructor method defined.", tp.GetCPPTypeString(), asmname.Name);

                    return nfo;
                }
                else if (name == "cctor")
                {
                    ConstructorInfo nfo = tp.GetConstructors(BindingFlags.Static).FirstOrDefault();

                    if (nfo == null)
                        return _err("The type `{1}::{0}` has no static constructor method defined.", tp.GetCPPTypeString(), asmname.Name).NULL();
                    else
                    {
                        if (!@static)
                            @static = _war("The keyword `new` has been ignored, as the requested method is a static one.") is int; // constant true

                        constructor = true;

                        return nfo;
                    }
                }
                else
                    return _err("The given member name `...//{0}` is not valid.{1}", name, helpstr).NULL();

                constructor = true;

                if (nfos.Length == 0)
                    _err("The type `{1}::{0}` has no constructor method(s) defined.", tp.GetCPPTypeString(), asmname.Name);
                else
                {
                    if (@static)
                        @static = _war("The keyword `new` is missing.") is string; // constant false

                    if (nfos.Length == 1)
                        return nfos[0];
                    else
                    {
                        Tuple<Type, ParameterAttributes>[] param = (from p in parameters select FetchParameter(p)).ToArray();
                        ConstructorInfo[] match = (from m in nfos
                                                   let margs = from p in m.GetParameters()
                                                               select new Tuple<Type, ParameterAttributes>(p.ParameterType, p.Attributes & (ParameterAttributes.In | ParameterAttributes.Out))
                                                   where new Func<bool>(() => {
                                                       Tuple<Type, ParameterAttributes>[] ma = margs.ToArray();

                                                       if (ma.Length != param.Length)
                                                           return false;

                                                       for (int i = 0; i < ma.Length; i++)
                                                           if (ma[i] == null || param[i] == null)
                                                               return false;
                                                           else if (ma[i].Item1 == null || param[i] == null)
                                                               return false;
                                                           else if (ma[i].Item2 != param[i].Item2)
                                                               return false;
                                                           else if (ma[i].Item1.FullName != param[i].Item1.FullName)
                                                               return false;

                                                       return true;
                                                   })()
                                                   select m).ToArray();

                        if (match.Length != 1)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("No constructor could be found inside the type `{1}::{0}`, which matches the given parameter list.", tp.GetCPPTypeString(), asmname.Name);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("However, the following constructors are defined inside the type `{1}::{0}`:");

                            for (int i = 0; i < nfos.Length; i++)
                                Console.WriteLine("    {0}", nfos[i].GetCPPSignature());

                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else
                            return match[0];
                    }
                }

                return null;
            }
            else
                return FetchMethod(tp, name, isprop, parameters);
#pragma warning restore
        }

        /// <summary>
        /// Parses the given brackets as array and returns the generated array type
        /// </summary>
        /// <param name="parent">Array element type</param>
        /// <param name="brackets">Brackets</param>
        /// <returns>Array type</returns>
        public static Type ParseArray(Type parent, string brackets)
        {
            Match m = Regex.Match(brackets, @"^(\s*\[\s*(?<commas>\,\s*)?\]\s*)");

            if (!m.Success)
                return parent;
            else
            {
                brackets = brackets.Remove(m.Index, m.Length);

                int commas = m.Groups["commas"].Success ? (from c in m.Groups["commas"].Value where c == ',' select false).Count() : 0;

                return ParseArray(Array.CreateInstance(parent, new int[commas + 1]).GetType(), brackets);
            }
        }

        /// <summary>
        /// Prints the given message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <returns>0</returns>
        public static int _err(string s)
        {
            return _err(s, null);
        }

        /// <summary>
        /// Prints the given formatted message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <param name="args">Format arguments</param>
        /// <returns>0</returns>
        public static int _err(string s, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            if (args == null)
                Console.WriteLine(s);
            else
                Console.WriteLine(s, args);

            Console.ForegroundColor = ConsoleColor.White;

            return 0;
        }

        /// <summary>
        /// Prints the given message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <returns>0</returns>
        public static int _war(string s)
        {
            return _war(s, null);
        }

        /// <summary>
        /// Prints the given formatted message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <param name="args">Format arguments</param>
        /// <returns>0</returns>
        public static int _war(string s, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            if (args == null)
                Console.WriteLine(s);
            else
                Console.WriteLine(s, args);

            Console.ForegroundColor = ConsoleColor.White;

            return 0;
        }

        /// <summary>
        /// Prints the given message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <returns>0</returns>
        public static int _ok(string s)
        {
            return _ok(s, null);
        }

        /// <summary>
        /// Prints the given formatted message string to the stdio-stream
        /// </summary>
        /// <param name="s">Format string</param>
        /// <param name="args">Format arguments</param>
        /// <returns>0</returns>
        public static int _ok(string s, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;

            if (args == null)
                Console.WriteLine(s);
            else
                Console.WriteLine(s, args);

            Console.ForegroundColor = ConsoleColor.White;

            return 0;
        }
    }

    /// <summary>
    /// Represents a member signature
    /// </summary>
    [Serializable]
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
