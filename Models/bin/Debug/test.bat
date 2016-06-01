::---------------------------------------------------------------------------------------------------------------------
::------------------------                   COPYRIGHT (C) 2016, UNKNOWN6656                   ------------------------
::---------------------------------------------------------------------------------------------------------------------
@echo off
title rundll.net test methods

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
echo Usage: %0 ^<valid^>   for valid tests
echo or     %0 ^<fail^>    for invalid tests
echo or     %0 ^<all^>     for all tests
goto end
::--------------------------------- INVALID tests (and syntax error test) follow here ---------------------------------
:error
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
rundll.net uclib MathFunction.//op*(MathFunction,MathFunction) "2x^3-7" "2-x^2"
:: test complex parsing
rundll.net uclib Complex.//op+(Complex,Complex) (1,0) (4,2)

:: test P/Invoke ref- and out-support
rundll.net uclib CoreLib.Win32.GetMasterVolume(^&float) 0
rundll.net uclib CoreLib.Win32.joyGetPosEx(int,^&JOYINFOEX) 0 new
rundll.net uclib CoreLib.Win32.GetBinaryType(string,^&BinaryType) "rundll.net.exe" new
:: test C++/CLR-support
rundll.net rundll.test.so TestModule::length(string) "foo/bar" --verbose
rundll.net rundll.test.so TestModule.getunion(float) 42.315
:: test c++/CLR native pointer support
rundll.net rundll.test.so TestModule.getpointer(int) 42
rundll.net rundll.test.so TestModule.getstruct(int,int) 42 315
:: test F#-support
rundll.net rundll.test.fs FSTestModule.Fibonacci(int) 20

:: test json loading support
:: rundll.net rundll.net.exe rundll.signature.FullString(rundll.signature) @JSON::testparam.json

:end