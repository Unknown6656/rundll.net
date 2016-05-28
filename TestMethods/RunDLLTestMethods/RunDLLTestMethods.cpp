#include "Stdafx.h"
#include "RunDLLTestMethods.h"

using namespace std;
using namespace RunDLL;

int TestModule::length(String^ str)
{
	return str->Length;
}

array<int>^ TestModule::getreg()
{
	int* ptr = __getreg();

	array<int>^ ret = {
		ptr[0],
		ptr[1],
		ptr[2],
		ptr[3],
	};

	return ret;
}

int* TestModule::getpointer(int value)
{
	return &value;
}

TestUnion^ TestModule::getunion(int value)
{
	TestUnion^ u = gcnew TestUnion();

	u->I = value;

	return u;
}

TestUnion^ TestModule::getunion(float value)
{
	TestUnion^ u = gcnew TestUnion();

	u->F = value;

	return u;
}

TestStruct* TestModule::getstruct(int i1, int i2)
{
	TestStruct s;

	s.I1 = i1;
	s.I2 = i2;

	return &s;
}

int* __getreg(void)
{
	int reg[4];

	__asm
	{
		MOV reg[0], EAX
		MOV reg[1], EBX
		MOV reg[2], ECX
		MOV reg[3], EDX
	}

	return reg;
}
