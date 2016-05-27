@echo off
title rundll.net test methods

:: tests follow here -----------------------------------------------------------------------------------

:: test parameterized dynamic constructors
rundll.net uclib.dll new math.mathfunction.//ctor(string) "3X2"
:: test static properties
rundll.net uclib.dll mathfunctions.x --v
:: test self-invokation
rundll.net rundll.net.exe Program.Main(string[]) {"rundll.net.exe","Program.Main(string[])",null}
:: test system library
rundll.net mscorlib.dll IntPtr::Size