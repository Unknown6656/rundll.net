#pragma once
#define _is System::Runtime::InteropServices

using namespace System;

namespace RunDLL
{
	[_is::StructLayoutAttribute(_is::LayoutKind::Explicit), SerializableAttribute]
	public ref struct TestUnion
	{
	public:
		[_is::FieldOffset(0)] int I;
		[_is::FieldOffset(0)] float F;
	};

	public struct TestStruct
	{
	public:
		int I1;
		int I2;
	};

	static public ref class TestModule
	{
	public:
		static int length(String^);
		static array<int>^ getreg();
		static int* getpointer(int);
		static TestUnion^ getunion(int);
		static TestUnion^ getunion(float);
		static TestStruct* getstruct(int, int);
	};
}

extern "C" __declspec(dllexport) int* __getreg(void);