#define TEST

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;
using System;

namespace MethodInvoker
{

    public static class Program
    {

        public static void Main(string[] args)
        {
#if TEST
            args = new string[] { Assembly.GetExecutingAssembly().Location, "Program", "test", "(string)", "\"test\"", "null" };
#endif
            Assembly asm = Assembly.LoadFrom(args[0]);
            IEnumerable<Type> tps = asm.GetTypes();
            IEnumerable<Type> classes = from t in tps
                                        where t.FullName.EndsWith(args[1])
                                        select t;
            IEnumerable<MethodInfo> methods = (from t in classes
                                               let m = t.GetMethods()
                                               select from _ in m
                                                      where _.Name == args[2]
                                                      select _).SelectMany(_ => _);

            if (methods.Count() > 1)
            {


            }
            else
                methods.First().Invoke(args.Contains("new") ? Activator.CreateInstance(methods.FirstOrDefault().DeclaringType) : null, /* parse args */);

        }

        public static int test(string s)
        {
            return s == null ? -1 : s.Length;
        }

        public static string test(int i)
        {
            return i < 0 ? null : new string('k', i);
        }
    }
}
