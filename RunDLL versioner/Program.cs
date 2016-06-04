using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.IO;
using System;

using CoreLib.Math.Encryption;
using CoreLib.Hardware;
using CoreLib;

namespace RunDLL
{
    using Properties;


    public static class Program
    {

        public static int Main(string[] args)
        {
            int[] vi = (from s in File.ReadAllText("./../../version.txt").Split('.')
                        select int.Parse(s)).ToArray();

            Version v = new Version(vi[0], vi[1], vi[2], Settings.Default.build);

            Settings.Default.build++;
            Settings.Default.Save();

            using (IHashAlgorithm hash = new U6Hash(HashSize.Ridicoulus))
                File.WriteAllText("./../../module.cs", @"
using System.Reflection;
using RunDLL;

[assembly: AssemblyVersion(""" + v + @""")]
[assembly: AssemblyFileVersion(""" + v + @""")]
[assembly: BuildInformation(
    Date = """ + DateTime.Now.ToString("yyyy-MM-dd, HH:mm:ss:ffffff") + @""",
    Machine = """ + new HwID(HashSize.Ridicoulus).GetHardwareID(true) + @""",
    Hash = """ + hash.ComputeHexHash(File.ReadAllText("./../../program.cs"), OMode.UpperCase, false) + @""",
    User = """ + hash.ComputeHexHash(Environment.UserName, OMode.UpperCase, false) + @"""
)]".TrimStart());

            return 0;
        }
    }
}
