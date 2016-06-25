::---------------------------------------------------------------------------------------------------------------------
::------------------------                   COPYRIGHT (C) 2016, UNKNOWN6656                   ------------------------
::---------------------------------------------------------------------------------------------------------------------
@echo off
title rundll.net test methods
color 0f

if "%1" == "valid" (
	cls
	goto valid
)
if "%1" == "invalid" (
	goto error
)
if "%1" == "fail" (
	goto error
)
if "%1" == "all" (
	goto error
)

:usage
cls
echo.
color 08
echo COPYRIGHT (C) 2016, UNKNOWN6656
echo .
color 04
echo Invalid or missing argument(s).
echo.
color 0e
echo Usage: %0 ^<valid^>   for valid tests
echo or     %0 ^<fail^>    for invalid tests
echo or     %0 ^<all^>     for all tests
color 0f
goto end
::--------------------------------- INVALID tests (and syntax error test) follow here ---------------------------------
:error
color 08
echo INVALID TEST CASES:
echo.
color 0f
cls
rundll.net --foo
rundll.net /dev/null 0
rundll.net kernel32 0
rundll.net mscorlib 0
rundll.net mscorlib int
rundll.net mscorlib int.//ctor()
rundll.net mscorlib int.Parse(int)
rundll.net mscorlib int.Parse(string)

if NOT "%1" == "all" (
	goto end
)
::---------------------------------------------- valid tests follow here ----------------------------------------------
:valid
color 08
echo VALID TEST CASES:
echo.
color 0f
:: test parameterized dynamic constructors
rundll.net uclib new math.mathfunction.//ctor(string) "3x^2"
rundll.net uclib new Complex.//ctor(double,double) 3 5
:: test static properties
rundll.net uclib mathfunctions.x --v
:: test self-invokation
rundll.net rundll.net Program.Main(string[]) {"rundll.net.exe","Program.Main(string[])",null}
:: test system library
rundll.net mscorlib IntPtr::Size
:: test implicit operators
rundll.net uclib ConstantMathFunction:://opimplicit(decimal) 4
:: test operators
rundll.net uclib h.Vector3D.//op+(h.Vector3D,h.Vector3D) (3,1,5) (4,2,0)
rundll.net uclib MathFunction.//op*(MathFunction,MathFunction) "2x^3-7" "2-x^2"
rundll.net uclib MathFunction.//op/(MathFunction,MathFunction) "x^2-2x+1" "x+1"
:: test complex parsing
rundll.net uclib Complex.//op+(Complex,Complex) (1,0) (4,2)

:: test P/Invoke ref- and out-support
rundll.net uclib CoreLib.Win32.GetMasterVolume(^&float) 0
rundll.net uclib CoreLib.Win32.joyGetPosEx(int,^&JOYINFOEX) 0 new
rundll.net uclib CoreLib.Win32.GetBinaryType(string,^&BinaryType) "rundll.net.exe" new
rundll.net uclib BitmapEffectFunctions.RGBtoHSL(byte,byte,byte,^&double,^&double,^&double) 31 250 42 0 0 0
:: test C++/CLR-support
rundll.net rundll.test.so TestModule::length(string) "foo/bar" --verbose
rundll.net rundll.test.so TestModule.getunion(float) 42.315
:: test C++/CLR native pointer support
rundll.net rundll.test.so TestModule.getpointer(int) 42
rundll.net rundll.test.so TestModule.getstruct(int,int) 42 315
:: test C# pointer support
rundll.net uclib "CommonLanguageRuntime.GetHandle(object)" 315.42f
rundll.net uclib "CommonLanguageRuntime.GetHandle<string>()"
:: test F#-support
rundll.net rundll.test.fs FSTestModule.Fibonacci(int) 20
:: test IL-support
rundll.net rundll.net RunDLL.Native.Add(int,int) 42 315
rundll.net rundll.net RunDLL.Native.Fibonacci(int) 10

:: test json loading support
:: rundll.net rundll.net.exe rundll.signature.FullString(rundll.signature) @JSON::testparam.json

:: test generic support
rundll.net uclib CommonLanguageRuntime.GetCPPTypeString(System.Type) "System.Collections.Generic.List<string>"
rundll.net uclib "Generic.BinaryTree<int>.//ctor(int[])" {3,1,5,4,2}
rundll.net uclib "LINQExtensions.Shuffle<int>(int[])" {3,1,5,4,2}
rundll.net uclib "LINQExtensions.GetTypes<int,string,char[]>()"
rundll.net rundll.net "Program.ParseParameter(string,System.Type,&dynamic)" {foo,bar,baz,top,kek,test,string} "System.Collections.Generic.List<string>" null
::--------------------------------------- TODO : FIX THE FOLLOWING CRASHES ---------------------------------------::
rundll.net uclib "LINQExtensions.GetTypes<int,string,char[],Tuple<Tuple<int,long[][]>,string[,]>>()"
rundll.net uclib "LINQExtensions.GetTypes<int,string,char[],string[,]>()"
:end