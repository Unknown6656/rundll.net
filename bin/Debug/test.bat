@echo off
title rundll.net test methods

:: tests follow here
cls & rundll.net uclib.dll new math.mathfunction.//ctor(string) "3X2"
pause
cls & rundll.net uclib.dll mathfunctions.get_x --v
pause
