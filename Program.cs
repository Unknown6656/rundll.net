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
using System.Numerics;
using System.Windows;
using System.CodeDom;
using System.Drawing;
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
using CoreLib.Math;
using CoreLib;

using env = global::System.Environment;
using win = global::System.Drawing;
using wpf = global::System.Windows;
using num = global::System.Numerics;
using cor = global::CoreLib.Math;
using System.ComponentModel;

namespace RunDLL
{

    /// <summary>
    /// The application's static class
    /// </summary>
    public unsafe static class Program
    {
        internal const string STR_REGEX_G_TYPEBASE = @"^\s*(?<class>([\w]+\.?)+)(\s*\<(?<params>(.+\s*(\,\s*)?)+)\>)\s*$";
        internal const string STR_REGEX_FLOAT = @"\s*(?<sign>\+|\-)?\s*(?<value>[0-9]*(\.|\,)?[0-9]+([e][\-\+]?[0-9]+)?)(d|f|m)?\s*";
        internal const string STR_REGEX_OPBASE = @"(true|false|implicit|explicit|\+\+|--|\|\||&&|<<=?|>>=?|->|~|[+-=!^<>*&|%]=?)";
        internal const string STR_REGEX_PARAMBASE = @"\s*(\s*\,?\s*((ref|out|in)\s+|\&\s*)?(\w+\.)*\w+(\s*((\[(\,)*\])+|\*+))?)+\s*";
        internal static readonly Regex REGEX_TYPE = new Regex(@"(?<namespace>(\w+\.)*)(?<class>\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static readonly Regex REGEX_METHOD = new Regex(@"((?<namespace>(\w+\.)*)(?<class>\w+\.))?((?<name>([a-z_]\w*|\/\/(c?c|d)tor))(?<parameters>\(" + STR_REGEX_PARAMBASE + @"\))?|(?<name>\/\/this)(?<parameters>\[" + STR_REGEX_PARAMBASE + @"\])?|(?<name>\/\/op" + STR_REGEX_OPBASE + @")(?<parameters>\(" + STR_REGEX_PARAMBASE + @"\))?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
        internal static readonly Dictionary<string, string> OPERATORS = CommonLanguageRuntime.operators.Reverse();
        internal static readonly IEnumerable<DirectoryInfo> ADD_PATHS = new List<DirectoryInfo>() {
            new DirectoryInfo(env.GetFolderPath(env.SpecialFolder.System)),
            new DirectoryInfo(env.GetFolderPath(env.SpecialFolder.SystemX86)),
            new DirectoryInfo(env.GetFolderPath(env.SpecialFolder.Windows)),
            new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()),
        };
        internal static readonly FileInfo nfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
        internal static readonly string modname = nfo.Name.ToLower().Replace(nfo.Extension.ToLower(), "").Trim('.', ' ', '\t', '\r', '\n');
        internal static readonly string helpstr = "Use '" + modname + " --help' for further reference.";
        internal static IEnumerable<Assembly> allasms;
        internal static IEnumerable<Type> alltypes;
        internal static AssemblyName asmname;
        internal static Assembly targasm;
        internal static FileInfo targnfo;


        /// <summary>
        /// Static constructor
        /// </summary>
        static Program()
        {
        }
        
        /// <summary>
        /// The application's entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        public static int Main(string[] args)
        {
            int retcode = 1;

            try
            {
                typeof(Program).GetMethod("LoadAsssembly").WarmUp();

                foreach (MethodInfo nfo in typeof(IEnumerable<>).GetMethods()
                                    .Union(typeof(Assembly).GetMethods()))
                    if (!nfo.IsAbstract)
                        nfo.WarmUp();

                retcode = InnerMain(args);
            }
            catch (Exception ex)
            {
                _err(PrintException(ex, 0));
            }
            finally
            {
                if (Debugger.IsAttached)
                    Win32.system("pause");
            }

            return retcode;
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
                sig.IsProperty = !reg.Groups["parameters"].Success && !(method.Contains('(') && method.Contains(')'));
            }
            else
                return _err("Invalid method name format `{1}`.{0}", helpstr, method);

            #endregion
            #region LOAD CLASS + MEMBER

            LoadAssemblies(args);

            Type @class = FetchGenericType(sig.FullClass);

            if (@class == null)
                return 0;

            _ok("Type `{0}` loaded from `{1}`.", @class.FullName.Replace(".", "::"), asmname.Name);

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

            if (!constructor)
                if (member.IsPInvoke())
                    _war("The member `[{1}] {0}` is an interface method to a native library. It is advised to call the native library directly using Microsoft's build-in `rundll32.exe`-application instead.", MemberName(result, constructor), asmname.Name);

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
                    Type T = pnfos[i].ParameterType;

                    cparameters[i] = ConvertType(parameters[i], T.PassedByReference() ? T.GetElementType() : T);
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
                Console.WriteLine(PrintException(ex, 0));
                Console.ForegroundColor = ConsoleColor.White;

                return 0;
            }

            PrintReturnValue(args, constructor, @static, @return, @class, member, cmember, cparameters);

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
        /// Prints the given exception
        /// </summary>
        /// <param name="_">Exception</param>
        /// <param name="i">Indentation level</param>
        /// <returns>Print string</returns>
        internal static string PrintException(Exception _, int i = 0)
        {
            return string.Join("\n", from s in string.Format("[HResult 0x{2:x8} {2}] '{0}'\n{5}\n{1}\n{4}\n{6}{3}",
                                                             _.Message,
                                                             _.StackTrace,
                                                             _.HResult,
                                                            (_.HelpLink ?? "").Length > 0 ? "\nSee `" + _.HelpLink + "` for more information..." : "",
                                                             _.Data.ToDebugString(),
                                                             _.TargetSite.GetCPPSignature(),
                                                             _.InnerException == null ? "" : "Inner exception:\n" + PrintException(_.InnerException, i + 1) + "\n")
                                                     .Split('\n')
                                     select new string(' ', i * 4) + s);
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
        /// <param name="cmember">Constructor member information</param>
        /// <param name="cparameters">The passed invocation parameters</param>
        public static void PrintReturnValue(string[] args, bool constructor, bool @static, object @return, Type @class, MethodInfo member, ConstructorInfo cmember, object[] cparameters)
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

                Func<Type, object, string> print = null;
                
                print = new Func<Type, object, string>((T, _) => {
                    if (_ == null)
                        return "nullptr";
                    else if ((T.IsPrimitive) ||
                             (_ is BigInteger) ||
                             (_ is num.Complex))
                        return _.ToString();
                    else if (T.IsPointer)
                    {
                        byte[] buffer = new byte[Marshal.SizeOf(T)];
                        void* ptr = Pointer.Unbox(_);
                        IntPtr addr = (IntPtr)ptr;
                        Type stype = T.GetElementType();
                        object obj = Marshal.PtrToStructure(addr, stype);

                        return string.Format("&[0x{0:x16}]: {1}", addr.ToInt64(), print(stype, obj));
                    }
                    else if (T.IsSerializable)
                        try
                        {
                            return T.GetCPPTypeString() + ": " + new JavaScriptSerializer().Serialize(_).FormatJSON();
                        }
                        catch
                        {
                            try
                            {
                                return Serialization.Serialize(_).FormatXML();
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
                                } catch { }

                                return _.var_dump(CheckPrintability("├─└╞═╘"/* <--ibm850 */), depth); //  "├─└╞═╘" /* <--unicode */
                            }
                        }
                    else if (_ is IEnumerable)
                    {
                        IEnumerable ien = _ as IEnumerable;
                        StringBuilder sb = new StringBuilder();
                        int cnt = 0;

                        sb.Append('{')
                          .AppendLine();

                        foreach (object c in ien)
                        {
                            sb.AppendFormat("[0x{0:x8}]:", cnt);

                            foreach (string l in Regex.Replace(print(c.GetType(), c), @"[\r\n]+", "\n").Split('\n'))
                                sb.Append("\n    ")
                                  .Append(l);

                            sb.AppendLine(",");

                            cnt++;
                        }

                        return sb.Append('}')
                                 .Insert(0, "(Size: 0x" + cnt.ToString("x8") + ") ")
                                 .ToString();
                    }
                    else
                        return _.ToString();
                });

                Console.WriteLine(print(constructor ? @class : member.ReturnType, @return));
                
                ParameterInfo[] pnfo = constructor ? cmember.GetParameters() : member.GetParameters();
                List<object> cparams = new List<object>();

                for (int i = 0, l = Math.Min(pnfo.Length, cparameters.Length); i < l; i++)
                    if (pnfo[i].Attributes.HasFlag(ParameterAttributes.Out) || pnfo[i].ParameterType.PassedByReference())
                        cparams.Add(cparameters[i]);

                if (cparams.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("\nParameters passed by reference:");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(print(typeof(IEnumerable<object>), cparams));
                }
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
                or destructor.  Use `.//op____` to invoke an operator, where
                ____ represents the operator token, which can be found on the
                website https://github.com/Unknown6656/rundll.net along with
                further information.
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
                     `System`, `System.Core` and `System.Numerics`).
                     Note: The library `mscorlib` is always included.
    -w, --wpflib   - Includes the .NET WPF (Windows Presentation Foundation)
                     framework libraries (`System.Xaml.dll`, `WindowsBase.dll`
                     `PresentationCore.dll`, `PresentationFramework.dll` and
                     `WindowsFormsIntegration.dll`).
    -f, --wformlib - Includes the .NET Windows Forms framework libraries
                     (`System.Drawing.dll`, `System.Windows.Forms.<*>.dll`).
    -c, --wcflib   - Includes the .NET WCF (Windows Communication Foundation)
                     framework libraries (`System.ServiceModel.<*>.dll`).
    -fs, --fsharp  - Includes the .NET F# framework libraries.
    -u, --uclib    - Includes the .NET Unknown6656 core library `uclib`.
    -e:, --extlib: - Includes the given .NET library and loads its types. The
                     assembly's file path must be given directly after the
                     colon (`:`). This option can be given multiple times.
    -h, --help     - Displays this help page.
                
Valid usage examples are:
    {0} mscorlib System.IntPtr.Size --depth2
    {0} /root/Documents/library.olb new ImgLib::Image::Rotate()
    {0} \\127.0.0.1\app.exe new MainWindow.//ctor(string) ""foobar"" --stdlib
".TrimEnd(), modname, nfo.Name);
                string[] lines = helpstr.Split('\n');

                if (Console.WindowHeight < lines.Length + 1)
                    Console.WindowHeight = lines.Length + 2;
                if (Console.WindowWidth < lines.Max(x => x.Length + 1))
                    Console.WindowWidth = lines.Max(x => x.Length + 2);

                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleExtensions.AdvancedAnimatedWriteLine(helpstr, 3f);
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
        /// Loads the file based on the given name
        /// </summary>
        /// <param name="name">File name</param>
        /// <returns>File</returns>
        public static FileInfo LoadFile(string name)
        {
            try
            {
                FileInfo nfo = new FileInfo(name);

                if (nfo.Exists)
                    return nfo;
            } catch { }

            string comppath = env.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) + ";" +
                              env.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) + ";" +
                              env.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + ";" +
                              new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName + ";" +
                              Directory.GetCurrentDirectory();

            IEnumerable<DirectoryInfo> dirs = (from p in comppath.Split(';')
                                               let d = new DirectoryInfo(p.Trim())
                                               where d.Exists
                                               select d).Union(ADD_PATHS);

            foreach (FileInfo f in dirs.SelectMany(_ => _.GetFiles('*' + name + '*')))
                try
                {
                    FileInfo nfo = new FileInfo(f.FullName);

                    if (nfo.Exists)
                        return nfo;
                } catch { }

            return null;
        }

        /// <summary>
        /// Loads the assembly based on the given name
        /// </summary>
        /// <param name="name">Assembly name</param>
        /// <returns>Assembly</returns>
        public static Assembly LoadAsssembly(string name)
        {
            try
            {
                return Assembly.LoadFrom(LoadFile(name).FullName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Loads the main assembly and displays basic and/or advanced information (based on the given command line arguments)
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Return code</returns>
        public static int LoadMainAssembly(string[] args)
        {
            targnfo = new FileInfo(args[0]);

            if (!targnfo.Exists)
                try
                {
                    targnfo = LoadFile(args[0]);
                }
                catch
                {
                    return _err("The file `{0}` could not not be found.", targnfo);
                }

            try
            {
                PEHeader hdr;

                try
                {
                    hdr = new PEHeader(targnfo.FullName);
                }
                catch
                {
                    // check for assemblies in the PE resource table (?)
                    // check for assemblies in the ELF resource table (?)
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
                asms.Add(typeof(BigInteger).Assembly);
            }

            if (CheckForOption(args, "wpflib", "w"))
            {
                asms.Add(typeof(XamlDirective).Assembly);
                asms.Add(typeof(WindowsFormsHost).Assembly);
                asms.Add(typeof(DependencyObject).Assembly);
                asms.Add(typeof(System.Windows.DataObject).Assembly);
                asms.Add(typeof(ConditionCollection).Assembly);
            }

            if (CheckForOption(args, "wformlib", "f"))
                asms.AddRange(from string s in new string[] { "System.Windows.Forms.dll", "System.Windows.Forms.DataVisualization.dll", "System.Windows.Forms.DataVisualization.Design.dll", "System.Drawing.dll", "System.Drawing.Design.dll" } select LoadAsssembly(s));

            if (CheckForOption(args, "wcflib", "c"))
                asms.AddRange(from string s in new string[] { "System.ServiceModel.Activation.dll", "System.ServiceModel.Activities.dll", "System.ServiceModel.Channels.dll", "System.ServiceModel.Discovery.dll", "System.ServiceModel.dll", "System.ServiceModel.Routing.dll", "System.ServiceModel.Web.dll", "System.ServiceProcess.dll" } select LoadAsssembly(s));

            if (CheckForOption(args, "wcflib", "c"))
                asms.AddRange(from string s in new string[] { "System.ServiceModel.Activation.dll", "System.ServiceModel.Activities.dll", "System.ServiceModel.Channels.dll", "System.ServiceModel.Discovery.dll", "System.ServiceModel.dll", "System.ServiceModel.Routing.dll", "System.ServiceModel.Web.dll", "System.ServiceProcess.dll" } select LoadAsssembly(s));

            if (CheckForOption(args, "fsharp", "fs"))
                asms.Add(LoadAsssembly("FSharp.Core.dll"));

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

            IEnumerable<AssemblyName> asmnames = (from _ in asms select _.GetReferencedAssemblies().Union(new AssemblyName[] { _.GetName() }))
                                                 .SelectMany(_ => _).Distinct();
            IList<Assembly> asmlist = new List<Assembly>();

            alltypes = asmnames.SelectMany(_ => {
                try
                {
                    Assembly asm = Assembly.Load(_);

                    asmlist.Add(asm);

                    return asm.GetTypes();
                }
                catch
                {
                    return new Type[0] { };
                }
            }).Distinct();
            allasms = asmlist.Distinct();
        }

        /// <summary>
        /// Converts the given object instance to the given type
        /// </summary>
        /// <param name="in">Source object</param>
        /// <param name="type">Target type</param>
        /// <returns>Converted object instance</returns>
        public static object ConvertType(object @in, Type type)
        {
            if ((type == null) && (@in == null))
                return null;
            else if (@in == null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            else if (type == null)
                return @in;

            if (type.IsArray)
            {
                Array src = @in as Array;
                Array dest = Array.CreateInstance(type.GetElementType(), src.Length);

                Array.Copy(src, dest, src.Length);

                return dest;
            }
            else if (type.IsPointer)
                return Pointer.Box(Pointer.Unbox(@in), type);
            // else if (type.IsNested)
            //     ;
            else if (@in is Type & type == typeof(Type))
                return @in as Type;
            else
                try
                {
                    return Convert.ChangeType(@in, type);
                }
                catch
                {

                    TypeConverter tp = TypeDescriptor.GetConverter(type);

                    return tp.ConvertFrom(@in);
                }
        }
        
        /// <summary>
        /// Fetches a type based on the namespace and typename
        /// </summary>
        /// <param name="namespace">Namespace name</param>
        /// <param name="class">Type name</param>
        /// <param name="forcegeneric">An optional paramater, which indicates, whether generic type search shall be forced</param>
        /// <returns>Type</returns>
        public static Type FetchType(string @namespace, string @class)
        {
            if ((@namespace.Trim().Length + @class.Trim().Length) == 0)
                return _err("The empty string is not a valid type name. Please enter the type name before the member name (separated by a dot `.` or a double colon `::`).").NULL() as Type;

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
            IEnumerable<Type> classes = (from a in allasms
                                         let t = a.GetType(__fclassnamenrm, false, true)
                                         where t != null
                                         select t).Union(
                                         from Type t in alltypes
                                         where t.FullName.ToLower().EndsWith(__fclassnamelwr)
                                         select t);

            if (classes.Count() == 0)
                _err("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm.Replace(".", "::"), asmname.Name, asmname.Version);
            else if (classes.Count() > 1)
            {
                IEnumerable<Type> oclasses = classes;

                classes = from Type t in alltypes
                          where t.Name == @class
                          where t.Namespace == @namespace
                          select t;

                if (classes.Count() == 0)
                {
                    classes = from Type t in alltypes
                              where t.Name == @class
                              select t;

                    if (classes.Count() != 1)
                    {
                        _err("The type `{0}` could not be found in the assembly `{1}[{2}]` or its dependencies.", __fclassnamenrm.Replace(".", "::"), asmname.Name, asmname.Version);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("However, the following types have been found, which partly match the given type name:");

                        int oclen = oclasses.Count();
                        const int ocmaxlen = 24;

                        foreach (Type t in oclasses.Take(ocmaxlen))
                            Console.WriteLine("    {0}", t.FullName.Replace(".", "::"));

                        if (oclen > ocmaxlen)
                        {
                            oclen -= ocmaxlen;

                            Console.WriteLine("{0} more entries follow. Do you want to show them? (y/N)", oclen);

                            if (char.ToLower(Console.ReadKey(false).KeyChar) == 'y')
                                foreach (Type t in oclasses.Skip(ocmaxlen))
                                    Console.WriteLine("    {0}", t.FullName.Replace(".", "::"));
                        }

                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                        return classes.First();
                }
                else if (classes.Count() > 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The type `{0}` could not be determined, as it is not unique:", @class.Replace(".", "::"));

                    foreach (Type t in classes)
                        Console.WriteLine("    [{1}]{0}", t.FullName.Replace(".", "::"), t.Assembly.CodeBase);

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
        /// Returns the generic/nested type associated withe the given type string
        /// </summary>
        /// <param name="typestring">Type string</param>
        /// <returns>Type</returns>
        public static Type FetchGenericType(string typestring)
        {
            Match m;

            typestring = typestring.Replace("::", ".").Trim();

            if ((m = Regex.Match(typestring, STR_REGEX_G_TYPEBASE)).Success)
            {
                string cls = m.Groups["class"].ToString();
                string par = m.Groups["params"].Success ? m.Groups["params"].ToString() : "";

                List<string> paratypes = new List<string>();

                int s = 0, e = 0, c = 0;

                for (int i = 0, l = par.Length; i < l; i++)
                    switch (par[i])
                    {
                        case ',':
                            if (c > 0)
                                goto default;

                            paratypes.Add(par.Substring(s, e));

                            s = i + 1;
                            e = 0;
                            break;
                        case '<': ++c;
                            goto default;
                        case '>': --c;
                            goto default;
                        default: ++e;
                            break;
                    }

                paratypes.Add(par.Substring(s, e));

                Type[] @params = (from type in paratypes
                                  where !string.IsNullOrWhiteSpace(type)
                                  select FetchGenericType(type)).ToArray();

                string pstring = @params.Length > 0 ? cls + '<' + new string(',', @params.Length - 1) + '>' : cls;
                string rstring = @params.Length > 0 ? string.Format("{0}`{1}[{2}]", cls, @params.Length,
                                                      string.Join(", ", from type in @params select "[" + type.FullName + ", " + type.Assembly.GetName().Name + "]")) : cls;

                return FetchType("", rstring) ??
                       FetchType("", pstring).MakeGenericType(@params);
            }
            else
                return FetchType("", typestring);
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

                if ((m = Regex.Match(argv, @"^\@xml\:\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
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
                else if ((m = Regex.Match(argv, @"^\@xml\:", RegexOptions.Compiled | RegexOptions.IgnoreCase)).Success)
                    try
                    {
                        xml = argv.Remove(m.Index, m.Length).Trim('"', ' ', '\t', '\r', '\n');
                        param = Serialization.Deserialize(xml, pnfo.ParameterType);
                    }
                    catch
                    {
                        return _err("The given string does not seem to be a valid XML string.{0}", helpstr);
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
                else if (!ParseParameter(argv, pnfo.ParameterType, out param))
                    return _err("The given argument `{0}` could not be interpreted as `{1}`.", argv.Trim(), pnfo.ParameterType.GetCPPTypeString());

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
        public static bool ParseParameter(string argv, Type type, out dynamic param)
        {
            bool isnum = false;
            bool isflt = false;
            Match m;

            try
            {
                if ((m = Regex.Match(argv, @"^\s*(?<sign>\+|\-)?\s*(0x(?<value>[0-9a-f]+)h?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    BigInteger ul = BigInteger.Parse(m.Groups["value"].ToString(), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);

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
                else if ((m = Regex.Match(argv, "^" + STR_REGEX_FLOAT + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled)).Success)
                {
                    decimal dec = decimal.Parse(m.Groups["value"].ToString().Replace('.', ','));

                    argv = (m.Groups["sign"].ToString().Contains('-') ? "-" : "") + dec;

                    isflt = true;
                }

                param = new TypeMatcher<object>()
                    .Case(T => T.PassedByReference(), (_, T) => {
                        Type t = T.GetElementType();
                        object v = null;

                        ParseParameter(argv, t, out v);

                        return ConvertType(v, t);
                    })
                    .Case(T => argv.ToLower().Trim() == "null", _ => null)
                    .Case<string>(argv)
                    .Case<char>(_ => {
                        if (argv.Length == 1)
                            return argv[0];

                        m = Regex.Match(argv, @"^\s*\'(?<value>(\\(b|r|n|t|a|f|v|0|\""|\'|\\|x[0-9a-f]{1,2}|u[0-9a-f]{1,4}))|[\u0000-\uffff])\'\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        return char.Parse(m.Groups["value"].ToString());
                    })
                    .Case<float, double, decimal>((_, T) => ConvertType(decimal.Parse(argv), T))
                    .Case<sbyte, byte, short, ushort, int, uint, long, ulong>((_, T) => ConvertType(argv.Contains('-') ? (object)long.Parse(argv) : (object)ulong.Parse(argv), T))
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

                                if (ParseParameter(s, t, out elem))
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
                            ParseParameter(argv.Remove(0, 1), typeof(long), out v);

                            return Pointer.Box((long*)(long)v, T);
                        }
                        else
                        {
                            Type t = T.GetElementType();

                            if (ParseParameter(argv, t, out v))
                                if (t.IsValueType)
                                {
                                    byte[] resv = new byte[Marshal.SizeOf(t)];

                                    fixed (byte* ptr = resv)
                                    {
                                        Marshal.StructureToPtr(v, (IntPtr)ptr, false);

                                        return Pointer.Box((void*)ptr, t);
                                    }
                                }
                                else
                                    v = GCHandle.Alloc(v);

                            return v;
                        }
                    })
                    .Case(T => Nullable.GetUnderlyingType(T) != null, (_, T) => {
                        if (Nullable.Equals(_, null) || (argv.ToLower() == "null"))
                            return null;
                        else
                        {
                            Type _t = Nullable.GetUnderlyingType(T);
                            object o = null;

                            ParseParameter(argv, _t, out o);

                            return o;
                        }
                    })
                    .Case<Type>(_ => FetchGenericType(argv))
                    .Case<BigInteger>(_ => BigInteger.Parse(argv))
                    .Case<DirectoryInfo>(_ => new DirectoryInfo(argv))
                    .Case<FileInfo>(_ => new FileInfo(argv))
                    .Case<Uri>(_ => new Uri(argv))
                    .Case<num.Complex, wpf.Size, wpf.Point, Vector2D, cor.Complex>((_, t) => {
                        if ((m = Regex.Match(argv, @"^\s*\(\s*(?<real>[^\s]+)\s*\,\s*(?<imag>[^\s]+)\s*\)\s*$")).Success)
                        {
                            object d1, d2;

                            if (ParseParameter(m.Groups["real"].ToString(), typeof(double), out d1) &&
                                ParseParameter(m.Groups["imag"].ToString(), typeof(double), out d2))
                                return t == typeof(wpf.Size) ? new wpf.Size((double)d1, (double)d2) as object :
                                    t == typeof(wpf.Point) ? new wpf.Point((double)d1, (double)d2) as object :
                                    t == typeof(num.Complex) ? new num.Complex((double)d1, (double)d2) as object :
                                    t == typeof(Vector2D) ? new Vector2D((double)d1, (double)d2) as object :
                                                            new cor.Complex((double)d1, (double)d2) as object;
                        }

                        return null;
                    })
                    .Case<Vector3D>((_, t) => {
                        if ((m = Regex.Match(argv, @"^\s*\(\s*(?<x>[^\s]+)\s*\,\s*(?<y>[^\s]+)\s*,\s*(?<z>[^\s]+)\s*\)\s*$")).Success)
                        {
                            object d1, d2, d3;

                            if (ParseParameter(m.Groups["x"].ToString(), typeof(double), out d1) &&
                                ParseParameter(m.Groups["y"].ToString(), typeof(double), out d2) &&
                                ParseParameter(m.Groups["z"].ToString(), typeof(double), out d3))
                                return new Vector3D((double)d1, (double)d2, (double)d3);
                        }

                        return null;
                    })
                    .Case<win.Size, win.Point>((_, t) => {
                        if ((m = Regex.Match(argv, @"^\s*\(\s*(?<x>[^\s]+)\s*\,\s*(?<y>[^\s]+)\s*\)\s*$")).Success)
                        {
                            object i1, i2;

                            if (ParseParameter(m.Groups["x"].ToString(), typeof(int), out i1) &&
                                ParseParameter(m.Groups["y"].ToString(), typeof(int), out i2))
                                return t == typeof(win.Size) ? new win.Size((int)i1, (int)i2) as object
                                                             : new win.Point((int)i1, (int)i2) as object;
                        }

                        return null;
                    })
                    .Case<SizeF, PointF>((_, t) => {
                        if ((m = Regex.Match(argv, @"^\s*\(\s*(?<x>[^\s]+)\s*\,\s*(?<y>[^\s]+)\s*\)\s*$")).Success)
                        {
                            object i1, i2;

                            if (ParseParameter(m.Groups["x"].ToString(), typeof(float), out i1) &&
                                ParseParameter(m.Groups["y"].ToString(), typeof(float), out i2))
                                return t == typeof(SizeF) ? new SizeF((float)i1, (float)i2) as object
                                                          : new PointF((float)i1, (float)i2) as object;
                        }

                        return null;
                    })
                    .Case<MathFunction>(_ => new MathFunction(argv))
                    .Case<ConstantMathFunction>(_ => new ConstantMathFunction(decimal.Parse(argv)))
                    .Default((_, T) => {
                        if (argv == "new")
                            return Activator.CreateInstance(T);
                        else if (T.IsAssignableFrom(typeof(List<>)))
                        {
                            Type t = T.GetNestedTypes()[0];
                            object o = null;

                            ParseParameter(argv, t.MakeArrayType(), out o);

                            object[] arr = ConvertType(o, typeof(object[])) as object[];

                            return arr.ToList();
                        }
                        // TODO : Fix IEnumerable<> parsing

                        return argv;
                    })
                    [type, argv];

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

            Type t = FetchGenericType(_p);

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
            bool indexer = name == "/this/";
            MethodInfo[] members = tp.GetMethods((BindingFlags)0x01063f7f);
            PropertyInfo[] props = tp.GetProperties();
            IEnumerable<MethodInfo> match = isprop ? from p in props
                                                     let pget = p.GetGetMethod() 
                                                     where indexer ? p.GetIndexParameters().Length > 0
                                                                   : p.Name == name ||
                                                                     pget.Name == name
                                                     select pget
                                                   : from m in members
                                                     where m.Name == name
                                                     select m;
            List<Tuple<Type, ParameterAttributes>> paramtypes = new List<Tuple<Type, ParameterAttributes>>();

            if (match.Count() > 0)
                if (match.Count() == 1)
                    return match.First();
                else
                {
                    Tuple<Type, ParameterAttributes> res;

                    foreach (string p in parameters)
                        if ((res = FetchParameter(p)) == null)
                            return null;
                        else
                            paramtypes.Add(res);

                    match = from m in match
                            let margs = from p in indexer ? (from i in props
                                                             where i.GetIndexParameters().Length > 0
                                                             where m == i.GetGetMethod()
                                                             select i).First().GetIndexParameters()
                                                          : m.GetParameters()
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
                        Console.WriteLine("The member `{0}` could not be found inside the type `[{1}]{2}`.", indexer ? "__index" : name, asmname.Name, tp.FullName.Replace(".", "::"));
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (match.Count() > 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The member `{0}` does match mulitple members inside `[{1}]{2}`:", indexer ? "__index" : name, asmname.Name, tp.FullName.Replace(".", "::"));
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
                Console.WriteLine("The member `{0}` could not be found inside the type `[{1}]{2}`.", indexer ? "__index" : name, asmname.Name, tp.FullName.Replace(".", "::"));
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

            if (Regex.Match(name, @"^\/\/(\w+|op" + STR_REGEX_OPBASE + ")$").Success)
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
                        _err("The type `[{1}] {0}` has no destructor method defined.", tp.GetCPPTypeString(), asmname.Name);

                    return nfo;
                }
                else if (name == "cctor")
                {
                    ConstructorInfo nfo = tp.GetConstructors(BindingFlags.Static).FirstOrDefault();

                    if (nfo == null)
                        return _err("The type `[{1}] {0}` has no static constructor method defined.", tp.GetCPPTypeString(), asmname.Name).NULL();
                    else
                    {
                        if (!@static)
                            @static = _war("The keyword `new` has been ignored, as the requested method is a static one.") is int; // constant true

                        constructor = true;

                        return nfo;
                    }
                }
                else if (name == "this")
                {
                    if (@static)
                        @static = _war("The keyword `new` is missing.") is string; // constant false

                    return FetchMethod(tp, "/this/", true, parameters);
                }
                else if (name.StartsWith("op"))
                {
                    if (!@static)
                        @static = _war("The keyword `new` has been ignored, as the requested method is a static one.") is int; // constant true

                    name = name.Remove(0, 2);

                    if (!OPERATORS.ContainsKey(name))
                        return _err("The static operator `{0}` could not be found inside the type `[{1}] {2}`", name, asmname.Name, tp.GetCPPTypeString()).NULL();
                    else
                        name = OPERATORS[name];

                    return FetchMethod(tp, name, false, parameters);
                }
                else
                    return _err("The given member name `...//{0}` is not valid.{1}", name, helpstr).NULL();

                constructor = true;

                if (nfos.Length == 0)
                    _err("The type `[{1}] {0}` has no constructor method(s) defined.", tp.GetCPPTypeString(), asmname.Name);
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
                            Console.WriteLine("No constructor could be found inside the type `[{1}] {0}`, which matches the given parameter list.", tp.GetCPPTypeString(), asmname.Name);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("However, the following constructors are defined inside the type `[{1}] {0}`:", tp.GetCPPTypeString(), asmname.Name);

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
