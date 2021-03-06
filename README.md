# rundll.net

## Introduction

The `rundll.net`-project is the .NET equivalent of Window's [`rundll.exe`/`rundll32.exe`](https://support.microsoft.com/en-us/kb/164787)-applications, which allows the caller to run functions inside a given dynamic library. The `rundll.net`-application, however, also the executions of private and static methods of the given [library/module/assembly](https://msdn.microsoft.com/en-us/library/ms973231.aspx).

The following image visualizes the basic functionality and execution timeline concept of the `rundll.net`-application:<br/>
![Functionality and Timeline](timeline.png)
<br/>

## Usage

The usage is defined as follows:
```
    rundll.net.exe <library> ['new'] <method> [arguments, ...] [options]
```

with the following parameters:

* `library`<br/>
  The [.NET assembly](https://msdn.microsoft.com/en-us/library/ms973231.aspx) file name or path
* `'new'`<br/>
  The [`new`](https://msdn.microsoft.com/en-us/library/fa0ab757.aspx)-keyword is optional, but must be passed if the method in question is not a static one (without the quotes).
* `method`<br/>
  Either a [fully qualified namespace, class and method signature](https://msdn.microsoft.com/en-us/library/dfb3cx8s.aspx) or the name of the class (if unique) followed by the unique method name (or optinal parameters to identify the method).<br/>The member name must be followed by parentheses (`(` and `)`) if the member in question is a function. Any parameter type musst be given inside the parentheses, seperated by commas (`,`). Generic parameters shall be passed before the parentheses and enclosed in angled brackets (`<` and `>`), seperated by commas. A [`ref-`](https://msdn.microsoft.com/en-us/library/14akc2c7.aspx) or [`out-`](https://msdn.microsoft.com/en-us/library/t3c3bfhx.aspx)Parameter must by given with a leading ampersand (`&`) or by the leading keywords `ref ` or `out ` _(Do please note that the ampersand must be escaped when used inside a scirpt or command line on Windows and Unix)_. Parameters which have been marked as [`params`](https://msdn.microsoft.com/en-us/library/w5zay9db.aspx) will be interpreted as regular arrays.<br/>Use `.//ctor`, `.//cctor`, `.//dtor` after the parent type to address the type's instance  constructor, static constructor or destructor. Use `.//op____` to invoke an operator, where `____` represents the operator token. Use `.//this[...]` to access the type's indexer property with the given parameter types `...` _(note the usage of brackets instead of parentheses)_.
* `arguments`<br/>
  An optional list of arguments (separated by commas without any whitespace), which will be passed as method parameters to the given method.<br/>
A serilaized XML- or JSON-string can be passed with `@XML:""....""` or `@JSON:""....""`, where `....` is the XML- or JSON-string in question. If the parameter shall bede serialized from a JSON- or XML-file, the argument shall be passed as `@JSON::""....""` or `@XML::""....""`, where `....` represents the path to the given JSON- or XML-file.
* `options`<br/>
  An optional list of global options.

The following global options are defined:

* `-d_`, `--depth_`<br/>Sets the return value print depth (the symbol `_` must be a positive integer value between 1 and 7).
* `-v`, `--verbose`<br/>Prints verbose information about the loaded assembly.
* `-s`, `--stdlib`<br/>Includes the .NET standard libraries ([`System.Data.dll`](http://referencesource.microsoft.com/#system.data,namespaces), [`System.dll`](http://referencesource.microsoft.com/#system,namespaces), [`System.Core.dll`](http://referencesource.microsoft.com/#system.core,namespaces) and [`System.Numerics`](http://referencesource.microsoft.com/#system.numerics,namespaces)).<br/>
  _Note: The .NET core framework library [`mscorlib.dll`](http://referencesource.microsoft.com/#mscorlib,namespaces) is always included._
* `-w`, `--wpflib`<br/>Includes the .NET WPF (Windows Presentation foundation) framework libraries ([`System.Xaml.dll`](http://referencesource.microsoft.com/#System.Xaml,namespaces), [`PresentationCore.dll`](http://referencesource.microsoft.com/#PresentationCore,namespaces), [`PresentationFramework.dll`](http://referencesource.microsoft.com/#PresentationFramework,namespaces), [`WindowsFormsIntegration.dll`](http://referencesource.microsoft.com/#WindowsFormsIntegration,namespaces) and [`WindowsBase.dll`](http://referencesource.microsoft.com/#WindowsBase,namespaces)).<br/>
* `-f`, `--wformlib`<br/>Includes the .NET Windows Forms framework libraries ([`System.Drawing.dll`](http://referencesource.microsoft.com/#System.Drawing,namespaces), [`System.Drawing.Design.dll`](http://referencesource.microsoft.com/#System.Drawing.Design,namespaces), [`System.Windows.Forms.dll`](http://referencesource.microsoft.com/#System.Windows.Forms,namespaces), `System.Windows.Forms.DataVisualization.dll` and `System.Windows.Forms.DataVisualization.Design.dll`).
* `-c`, `--wcflib`<br/>Includes the .NET WCF (Windows Communication Foundation) framework libraries (`System.ServiceModel.*.dll`).
* `-fs`, `--fsharp`<br/>Includes the .NET F# framework libraries.
* `-u`, `--uclib`<br/>Includes the .NET Unknown6656 core library `uclib`
* `-e:...`, `--extlib:...`<br/>Includes the given .NET library and loads its types. The assembly's file path must be given directly after the colon (`:`). This option can be given multiple times.
* `-t ...`, `--test ...`<br/>Executes the given test case(s), which are stored in the batchfile [`test.bat`](https://github.com/Unknown6656/rundll.net/blob/master/bin/Debug/test.bat). _NOTE: The batchfile is not required to be accessible, as the application stores a copy of the test cases_.
* `-h`, `--help`<br/>Displays the help page.

_NOTE: Any other dependant library will automatically be loaded by the `rundll.net`-application if required._

The following operatore tokens are defined:

 - `//op+` &#160; &#160; The mathematical addition operator `+`
 - `//op-` &#160; &#160; The mathematical subtraction operator `-`
 - `//op*` &#160; &#160; The mathematical multiplication operator `*`
 - `//op/` &#160; &#160; The mathematical division operator `/`
 - `//op%` &#160; &#160; The mathematical modulos operator `%`
 - `//op+=` &#160; &#160; The mathematical addition assignment operator `+=`
 - `//op-=` &#160; &#160; The mathematical subtraction assignment operator `-=`
 - `//op*=` &#160; &#160; The mathematical multiplication assignment operator `*=`
 - `//op/=` &#160; &#160; The mathematical division assignment operator `/=`
 - `//op%=` &#160; &#160; The mathematical modulos assignment operator `%=`
 - `//op++` &#160; &#160; The unary mathematical increacement operator `++`
 - `//op--` &#160; &#160; The unary mathematical decreacement operator `--`
 - `//op!` &#160; &#160; The unary boolean inversion operator `!`
 - `//op~` &#160; &#160; The unary bitwise inversion operator `~`
 - `//op<<` &#160; &#160; The bitwise left shift operator `<<`
 - `//op>>` &#160; &#160; The bitwise right shift operator `>>`
 - `//op<<=` &#160; &#160; The bitwise left shift assignment operator `<<=`
 - `//op>>=` &#160; &#160; The bitwise right shift assignment operator `>>=`
 - `//op==` &#160; &#160; The equality operator `==`
 - `//op!=` &#160; &#160; The inequality operator `!=`
 - `//op=` &#160; &#160; The assignment operator `=`
 - `//op->` &#160; &#160; The member selection operator `->`
 - `//op->*` &#160; &#160; The member pointer selection operator `->*`
 - `//op^` &#160; &#160; The bitwise XOR-operator `^`
 - `//op|` &#160; &#160; The bitwise OR-operator `|`
 - `//op&` &#160; &#160; The bitwise AND-operator `&`
 - `//op||` &#160; &#160; The boolean OR-operator `||`
 - `//op&&` &#160; &#160; The boolean AND-operator `&&`
 - `//op^=` &#160; &#160; The bitwise XOR-assignment operator `^=`
 - `//op|=` &#160; &#160; The bitwise OR-assignment operator `|=`
 - `//op&=` &#160; &#160; The bitwise AND-assignment operator `&=`
 - `//optrue` &#160; &#160; The cast operator `true`
 - `//opfalse` &#160; &#160; The cast operator `false`
 - `//opimplicit` &#160; &#160; The cast operator `implicit`
 - `//opexplicit` &#160; &#160; The cast operator `explicit`

If a generic parameter is passed as argument to a function or a generic type parameter is required for the function or its parent type, the C#-notation shall be used:
<br/> &#160; &#160; &#160; &#160; &#160; &#160; &#160;`rundll.net library.so Namespace.Class<T>.Method<U>(Type<V>) ...`
<br/>_**NOT**_: &#160; `rundll.net library.so Namespace.Class(Of T).Method(Of U)(Type(Of V)) ...`

Valid usage examples are:
```
    rundll.net mscorlib System.IntPtr.Size -d2
    rundll.net /root/Documents/library.olb new ImgLib::Image::Rotate()
    rundll.net \\127.0.0.1\Shares\Public\app.exe new MainWindow.//ctor(string) ""foobar"" --stdlib
```

The batch file [`test.bat`](https://github.com/Unknown6656/rundll.net/blob/master/test.bat) contains a list of test cases with different usage syntaxes, which can be used as a guide.

## Changelog

 - **2016-06-25** Version _1.2.0_:
<br/>&#43; Added the possibility to execute the stored test cases without the requirement of the batch file [`test.bat`](https://github.com/Unknown6656/rundll.net/blob/master/test.bat)
 - **2016-06-25** Version _1.1.4_:
<br/>&#42; Fixed uclib-side out-parameter bugs
 - **2016-06-12** Version _1.1.3_:
<br/>&#42; Fixed bugs concerning generic type parsing, when the generic arguments were passed as part of the parent type before the constructor string token
 - **2016-06-05** Version _1.1.2_:
<br/>&#43; Added generic list parsing support
 - **2016-06-05** Version _1.1.1_:
<br/>&#42; Fixed some generic array parsing bugs (but not yet deeply nested generic types)
 - **2016-06-05** Version _1.1.0 (Generic update)_:
<br/>&#43; Added generic method support
 - **2016-06-04** Version _1.0.2_:
<br/>&#43; Added internal CIL code support
 - **2016-06-04** Version _1.0.1_:
<br/>&#43; Added a unique hash value to the assembly attributes for version/build tracking
<br/>&#42; Improved the versioner application
 - **2016-06-03** Version _1.0.0_:
<br/>First beta release of the `rundll.net`-project
