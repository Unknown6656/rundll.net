# rundll.net

##Introduction

The `rundll.net`-project is the .NET equivalent of Window's [`rundll.exe`/`rundll32.exe`](https://support.microsoft.com/en-us/kb/164787)-applications, which allows the caller to run functions inside a given dynamic library. The `rundll.net`-application, however, also the executions of private and static methods of the given [library/module/assembly](https://msdn.microsoft.com/en-us/library/ms973231.aspx).

##Usage

The usage is defined as follows:
```
    rundll.net.exe [options] <library> ['new'] <method> [arguments, ...]
```

with the following parameters:

* `libaray`<br/>
  The [.NET assembly](https://msdn.microsoft.com/en-us/library/ms973231.aspx) file name or path
* `'new'`<br/>
  The [`new`](https://msdn.microsoft.com/en-us/library/fa0ab757.aspx)-keyword is optional, but must be passed if the method in question is not a static one (without the quotes).
* `method`<br/>
  Either a [fully qualified namespace, class and method signature](https://msdn.microsoft.com/en-us/library/dfb3cx8s.aspx) or the name of the class (if unique) followed by the unique method name (or optinal parameters to identify the method).
* `arguments`<br/>
  An optional list of arguments (separated by commas without any whitespace), which will be passed as method parameters to the given method.<br/>
A serilaized XML- or JSON-string can be passed with `@XML:""....""` or `@JSON:""....""`, where `....` is the XML- or JSON-string in question. If the parameter shall bede serialized from a JSON- or XML-file, the argument shall be passed as `@JSON::""....""` or `@XML::""....""`, where `....` represents the path to the given JSON- or XML-file.
* `options`<br/>
  An optional list of global options, which can be used at any position inside the commandline

The following global options are defined:

* `-v`, `--verbose`<br/>Prints verbose information about the loaded assembly.
* `-s`, `--stdlib`<br/>Includes the .NET standard libraries ([`System.Data.dll`](http://referencesource.microsoft.com/#system.data,namespaces), [`System.dll`](http://referencesource.microsoft.com/#system,namespaces) and [`System.Core.dll`](http://referencesource.microsoft.com/#system.core,namespaces)).<br/>
  _Note: The library [`mscorlib.dll`](http://referencesource.microsoft.com/#mscorlib,namespaces) is always included._
* `-u`, `--uclib`<br/>Includes the .NET library `uclib`
* `-h`, `--help`<br/>Displays this help page.
                
Valid usage examples are:
```
    rundll.net.exe mscorlib.dll System.IntPtr.Size
    rundll.net.exe /root/Documents/library.olb new ImgLib.Image.Rotate()
    rundll.net.exe \\127.0.0.1\Public\app.exe new MainWindow..ctor(string) ""foobar"" --stdlib
```
