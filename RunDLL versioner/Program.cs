using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.IO;
using System;

namespace RunDLL
{
    using Properties;


    public static class Program
    {

        public static int Main(string[] args)
        {
            Version v = new Version(1, 0, 0, Settings.Default.build);

            Settings.Default.build++;
            Settings.Default.Save();

            File.WriteAllText("./../../module.cs", @"
using System.Reflection;

[assembly: AssemblyVersion(""" + v + @""")]
[assembly: AssemblyFileVersion(""" + v + @""")]
");

            return 0;
        }
    }
}
