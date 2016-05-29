@echo off
title rundll.net test methods

:: tests follow here -----------------------------------------------------------------------------------

:: test parameterized dynamic constructors
rundll.net uclib.dll new math.mathfunction.//ctor(string) "3x^2"
:: test static properties
rundll.net uclib.dll mathfunctions.x --v
:: test self-invokation
rundll.net rundll.net.exe Program.Main(string[]) {"rundll.net.exe","Program.Main(string[])",null}
:: test system library
rundll.net mscorlib.dll IntPtr::Size
:: test implicit operators
rundll.net uclib.dll ConstantMathFunction:://opimplicit(decimal) 4
:: test other operators

:: test P/Invoke ref- and out-support
rundll.net uclib.dll CoreLib.Win32.GetMasterVolume(&float) 0
rundll.net uclib.dll CoreLib.Win32.joyGetPosEx(int,&JOYINFOEX) 0 new
rundll.net uclib.dll CoreLib.Win32.GetBinaryType(string,&BinaryType) "rundll.net.exe" new
:: test C++/CLR-support
rundll.net rundll.test.so TestModule::length(string) "foo/bar" --verbose
rundll.net rundll.test.so TestModule.getunion(float) 42.315
:: test c++/CLR native pointer support
rundll.net rundll.test.so TestModule.getpointer(int) 42
rundll.net rundll.test.so TestModule.getstruct(int,int) 42 315
:: test F#-support
rundll.net rundll.test.fs.dll FSTestModule.Fibonacci(int) 10

:: test json loading support
:: rundll.net rundll.net.exe rundll.signature.FullString(rundll.signature) @JSON::testparam.json
