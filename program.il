// .corflags 0x0000002
// .vtfixup [1] int32 fromunmanaged at VT_01
// .vtfixup [2] int32 fromunmanaged at VT_02
// .data VT_01 = int32(0)
// .data VT_02 = int32(0)

.class public sequential ansi sealed beforefieldinit RunDLL.Native extends [mscorlib]System.ValueType
{
	.pack 0
	.size 0
	.method private static hidebysig specialname rtspecialname int32 Add(int32 a, int32 b)
	{
		// .vtentry 1:1
		// .export[1]
		.maxstack 2
		ldarg.0
		ldarg.1
		add
		ret
	}
	.method private static hidebysig specialname rtspecialname int32 Fibonacci(int32 i) cil managed
	{
		// .vtentry 2:2
		// .export[2]
		.maxstack 5
		.locals init(
			[0] int32 a
		)
  L_00: ldarg.0
		brtrue.s L_06
		ldloc.0
		ret
  L_06: ldloc.0
		ldarg.0
		add
		stloc.0
		ldarg.0
		ldc.i4.1
		sub
		starg.s i
		br.s L_00
	}
}
