#include "Stdafx.h"
#include "RunDLLTestMethods.h"

using namespace std;

int RunDLL::TestModule::length(String^ str)
{
	return str->Length;
}
