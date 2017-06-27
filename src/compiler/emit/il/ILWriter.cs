using System;
using System.Reflection;
using System.Reflection.Emit;

using static Utils;

/**
Friendlier wrapper around ILGenerator.
*/
sealed class ILWriter {
	//readonly Arr.Builder<string> logs;
	readonly ILGenerator il;
	readonly bool isStatic;

	internal ILWriter(MethodBuilder mb) {
		il = mb.GetILGenerator();
		isStatic = mb.IsStatic;
	}

	internal ILWriter(ConstructorBuilder cb) {
		il = cb.GetILGenerator();
		isStatic = false;
	}

	ILWriter(ILGenerator il, bool isStatic) {
		this.il = il;
		this.isStatic = isStatic;
	}

	static void log(string s) {
		//Console.WriteLine("  " + s);
		//logs.add(s);
	}

	internal void pop() {
		il.Emit(OpCodes.Pop);
	}

	internal void ret() {
		log("return");
		il.Emit(OpCodes.Ret);
	}

	internal void constInt(int i) {
		log($"const {i}");
		il.Emit(OpCodes.Ldc_I4, i);
	}

	internal void constDouble(double d) {
		log($"const {d}");
		il.Emit(OpCodes.Ldc_R8, d);
	}

	internal void constStr(string s) {
		log($"const '{s}'");
		il.Emit(OpCodes.Ldstr, s);
	}

	internal void loadStaticField(FieldInfo field) {
		log($"load static field {field.DeclaringType.Name}: {field.Name}");
		il.Emit(OpCodes.Ldsfld, field);
	}

	internal void callStaticMethod(MethodInfo m) {
		log($"call static {m.DeclaringType.Name}: {m}");
		assert(m.IsStatic);
		// Last arg only matters if this is varargs.
		il.EmitCall(OpCodes.Call, m, null);
	}

	internal void callConstructor(ConstructorInfo ctr) {
		il.Emit(OpCodes.Newobj, ctr);
	}

	/** Remember to call markLabel! */
	internal Label label() => il.DefineLabel();

	internal void markLabel(Label l) {
		log($"{l}");
		il.MarkLabel(l);
	}

	internal void getThis() {
		assert(!isStatic);
		il.Emit(OpCodes.Ldarg_0);
	}

	internal void setField(FieldInfo field) {
		log($"set instance field {field.Name}");
		il.Emit(OpCodes.Stfld, field);
	}

	internal void getField(FieldInfo field) {
		log($"get instance field {field.Name}");
		il.Emit(OpCodes.Ldfld, field);
	}

	internal void goToIfFalse(Label l) {
		log($"goto if false: {l}");
		il.Emit(OpCodes.Brfalse, l);
	}

	internal void goTo(Label l) {
		log($"goto {l}");
		il.Emit(OpCodes.Br, l);
	}

	internal void getParameter(uint index) {
		log($"get parameter {index}");
		il.Emit(ldargOperation(index, isStatic));
	}

	static OpCode ldargOperation(uint index, bool isStatic) {
		if (!isStatic) index++;
		switch (index) {
			case 0:
				return OpCodes.Ldarg_0;
			case 1:
				return OpCodes.Ldarg_1;
			case 2:
				return OpCodes.Ldarg_2;
			case 3:
				return OpCodes.Ldarg_3;
			default:
				throw TODO();
		}
	}

	internal void callInstanceMethod(MethodInfo mi, bool isVirtual) {
		assert(isVirtual == mi.IsVirtual); //TODO: remove that parameter then?
		var opcode = isVirtual ? OpCodes.Callvirt : OpCodes.Call;
		il.Emit(opcode, mi);
	}

	/** Should be called after pushing the locals' initial value on the stack. */
	internal Local initLocal(Type type) {
		var lb = il.DeclareLocal(type);
		il.Emit(OpCodes.Stloc, lb);
		return new Local(lb);
	}

	internal void getLocal(Local local) {
		il.Emit(OpCodes.Ldloc, local.__builder);
	}

	/** Treat this as private! Do not inspec contents if you are not ILWriter! */
	internal struct Local {
		internal readonly LocalBuilder __builder;
		internal Local(LocalBuilder builder) { __builder = builder; }
	}
}
