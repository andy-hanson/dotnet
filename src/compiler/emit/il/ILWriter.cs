using System;
using System.Reflection;
using System.Reflection.Emit;

using static Utils;

//mv?
interface Logger {
	void log(string s);
}

/**
Friendlier wrapper around ILGenerator.
*/
struct ILWriter {
	readonly ILGenerator il;
	readonly bool isStatic;
	readonly /*nullable*/ Logger logger;

	internal ILWriter(MethodBuilder mb) : this(mb, null) {}
	internal ILWriter(MethodBuilder mb, /*nullable*/ Logger logger) {
		il = mb.GetILGenerator();
		isStatic = mb.IsStatic;
		this.logger = logger;
	}

	internal ILWriter(ConstructorBuilder cb) : this(cb, null) {}
	internal ILWriter(ConstructorBuilder cb, /*nullable*/ Logger logger) {
		il = cb.GetILGenerator();
		isStatic = false;
		this.logger = logger;
	}

	//kill
	internal void gotoIfEqual(Label l) {
		il.Emit(OpCodes.Beq, l.__inner);
	}
	internal void not() {
		il.Emit(OpCodes.Not);
	}
	internal void log(Local l) {
		il.EmitWriteLine(l.__builder);
	}
	internal void sub() => il.Emit(OpCodes.Sub);

	internal void pop() {
		logger?.log(nameof(pop));
		il.Emit(OpCodes.Pop);
	}

	internal void ret() {
		logger?.log("return");
		il.Emit(OpCodes.Ret);
	}

	internal void constUint(uint u) {
		logger?.log($"const uint {u}");
		il.Emit(OpCodes.Ldc_I4, u);
	}

	internal void constInt(int i) {
		logger?.log($"const int {i}");
		il.Emit(OpCodes.Ldc_I4, i);
	}

	internal void constDouble(double d) {
		logger?.log($"const double {d}");
		il.Emit(OpCodes.Ldc_R8, d);
	}

	internal void constString(string s) {
		logger?.log($"const string \"{s}\"");
		il.Emit(OpCodes.Ldstr, s);
	}

	internal void loadStaticField(FieldInfo field) {
		logger?.log($"load static field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldsfld, field);
	}

	internal void callConstructor(ConstructorInfo ctr) {
		logger?.log($"new {ctr.DeclaringType.Name}");
		il.Emit(OpCodes.Newobj, ctr);
	}

	/** Remember to call markLabel! */
	internal Label label(Sym name) =>
		new Label(il.DefineLabel(), name);

	internal void markLabel(Label l) {
		logger?.log($"{l.name.str}:");
		il.MarkLabel(l.__inner);
	}

	internal void getThis() {
		// This will work for either a regular instance method or for a static method with a synthetic 'this' argument
		logger?.log("this");
		il.Emit(OpCodes.Ldarg_0);
	}

	internal void getField(FieldInfo field) {
		logger?.log($"get instance field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldfld, field);
	}

	internal void setField(FieldInfo field) {
		logger?.log($"set instance field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Stfld, field);
	}

	internal void goToIfFalse(Label lbl) {
		logger?.log($"goto if false: {lbl.name.str}");
		il.Emit(OpCodes.Brfalse, lbl.__inner);
	}

	internal void goToIfTrue(Label lbl) {
		logger?.log($"goto if true: {lbl.name.str}");
		il.Emit(OpCodes.Brtrue, lbl.__inner);
	}

	internal void goTo(Label l) {
		logger?.log($"goto {l.name.str}");
		il.Emit(OpCodes.Br, l.__inner);
	}

	internal void getParameter(uint index) {
		logger?.log($"get parameter {index}");
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

	internal void call(MethodInfo method, bool isVirtual) {
		if (isVirtual)
			callVirtual(method);
		else
			callNonVirtual(method);
	}

	internal void callNonVirtual(MethodInfo method) {
		logger?.log($"call non-virtual {method.DeclaringType.Name}.{method.Name}");
		il.Emit(OpCodes.Call, method);
	}

	internal void callVirtual(MethodInfo method) {
		logger?.log($"call virtual {method.DeclaringType.Name}.{method.Name}");
		il.Emit(OpCodes.Callvirt, method);
	}

	internal void tailcallNonVirtual(MethodInfo method) {
		logger?.log($"tail call non-virtual {method.DeclaringType.Name}.{method.Name}");
		il.Emit(OpCodes.Tailcall);
		il.Emit(OpCodes.Call, method);
	}

	internal Local declareLocal(Type type, Sym name) {
		logger?.log($"declare local {name.str}");
		return new Local(il.DeclareLocal(type), name);
	}

	/** Should be called after pushing the locals' initial value on the stack. */
	internal Local initLocal(Type type, Sym name) {
		var local = declareLocal(type, name);
		setLocal(local);
		return local;
	}

	internal void getLocal(Local local) {
		logger?.log($"get local {local.name.str}");
		il.Emit(OpCodes.Ldloc, local.__builder);
	}

	internal void setLocal(Local local) {
		logger?.log($"set local {local.name.str}");
		il.Emit(OpCodes.Stloc, local.__builder);
	}

	internal void doThrow() {
		logger?.log("throw");
		il.Emit(OpCodes.Throw);
	}

	static readonly Sym symEndTry = Sym.of("end try");
	internal Label beginTry() {
		logger?.log("begin try");
		return new Label(il.BeginExceptionBlock(), symEndTry);
	}

	internal void beginCatch(Type exceptionType) {
		logger?.log($"catch {exceptionType}");
		il.BeginCatchBlock(exceptionType);
	}

	internal void endTry() {
		logger?.log("end try");
		il.EndExceptionBlock();
	}

	/** Treat this as private! Do not inspec contents if you are not ILWriter! */
	internal struct Local {
		internal readonly LocalBuilder __builder;
		internal readonly Sym name; // For log output
		internal Local(LocalBuilder builder, Sym name) { __builder = builder; this.name = name; }
	}

	internal struct Label {
		internal System.Reflection.Emit.Label __inner;
		internal readonly Sym name; // For log output
		internal Label(System.Reflection.Emit.Label inner, Sym name) { __inner = inner; this.name = name; }
	}
}
