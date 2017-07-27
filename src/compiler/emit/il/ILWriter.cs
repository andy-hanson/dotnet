using System;
using System.Reflection;
using System.Reflection.Emit;

using static Utils;

interface InstructionLogger {
	StringMaker log();
}

static class LogUtils {
	internal static StringMaker addField(this StringMaker s, FieldInfo f) =>
		s.add(f.DeclaringType.Name).add('.').add(f.Name);

	internal static StringMaker addMethod(this StringMaker s, MethodInfo m) =>
		s.add(m.DeclaringType.Name).add('.').add(m.Name);
}

/**
Friendlier wrapper around ILGenerator.
*/
struct ILWriter {
	readonly ILGenerator il;
	readonly bool isStatic;
	readonly /*nullable*/ InstructionLogger logger;

	internal ILWriter(MethodBuilder mb) : this(mb, null) {}
	internal ILWriter(MethodBuilder mb, /*nullable*/ InstructionLogger logger) {
		il = mb.GetILGenerator();
		isStatic = mb.IsStatic;
		this.logger = logger;
	}

	internal ILWriter(ConstructorBuilder cb) : this(cb, null) {}
	internal ILWriter(ConstructorBuilder cb, /*nullable*/ InstructionLogger logger) {
		il = cb.GetILGenerator();
		isStatic = false;
		this.logger = logger;
	}

	internal void pop() {
		logger?.log().add(nameof(pop));
		il.Emit(OpCodes.Pop);
	}

	internal void ret() {
		logger?.log().add("return");
		il.Emit(OpCodes.Ret);
	}

	internal void constUint(uint u) {
		logger?.log().add("const uint ").add(u);
		il.Emit(OpCodes.Ldc_I4, u);
	}

	internal void constInt(int i) {
		logger?.log().add("const int ").add(i);
		il.Emit(OpCodes.Ldc_I4, i);
	}

	internal void constDouble(double d) {
		logger?.log().add("const double ").add(d);
		il.Emit(OpCodes.Ldc_R8, d);
	}

	internal void constString(string s) {
		logger?.log().add("const string ").addQuotedString(s);
		il.Emit(OpCodes.Ldstr, s);
	}

	internal void loadStaticField(FieldInfo field) {
		logger?.log().add("load static field ").addField(field);
		il.Emit(OpCodes.Ldsfld, field);
	}

	internal void callConstructor(ConstructorInfo ctr) {
		logger?.log().add("new ").add(ctr.DeclaringType.Name);
		il.Emit(OpCodes.Newobj, ctr);
	}

	/** Remember to call markLabel! */
	internal Label label(Sym name) =>
		new Label(il.DefineLabel(), name);

	internal void markLabel(Label l) {
		logger?.log().add(l.name.str).add(':');
		il.MarkLabel(l.__inner);
	}

	internal void getThis() {
		// This will work for either a regular instance method or for a static method with a synthetic 'this' argument
		logger?.log().add("this");
		il.Emit(OpCodes.Ldarg_0);
	}

	internal void getField(FieldInfo field) {
		logger?.log().add("get instance field ").addField(field);
		il.Emit(OpCodes.Ldfld, field);
	}

	internal void setField(FieldInfo field) {
		logger?.log().add("set instance field ").addField(field);
		il.Emit(OpCodes.Stfld, field);
	}

	internal void goToIfFalse(Label lbl) {
		logger?.log().add("goto if false: ").add(lbl.name.str);
		il.Emit(OpCodes.Brfalse, lbl.__inner);
	}

	internal void goToIfTrue(Label lbl) {
		logger?.log().add("goto if true: ").add(lbl.name.str);
		il.Emit(OpCodes.Brtrue, lbl.__inner);
	}

	internal void goTo(Label l) {
		logger?.log().add("goto ").add(l.name.str);
		il.Emit(OpCodes.Br, l.__inner);
	}

	internal void getParameter(uint index) {
		logger?.log().add("get parameter ").add(index);
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
		logger?.log().add("call non-virtual ").addMethod(method);
		il.Emit(OpCodes.Call, method);
	}

	internal void callVirtual(MethodInfo method) {
		logger?.log().add("call virtual ").addMethod(method);
		il.Emit(OpCodes.Callvirt, method);
	}

	internal void tailcallNonVirtual(MethodInfo method) {
		logger?.log().add("tail call non-virtual ").addMethod(method);
		il.Emit(OpCodes.Tailcall);
		il.Emit(OpCodes.Call, method);
	}

	internal Local declareLocal(Type type, Sym name) {
		logger?.log().add("declare local ").add(name.str);
		return new Local(il.DeclareLocal(type), name);
	}

	/** Should be called after pushing the locals' initial value on the stack. */
	internal Local initLocal(Type type, Sym name) {
		var local = declareLocal(type, name);
		setLocal(local);
		return local;
	}

	internal void getLocal(Local local) {
		logger?.log().add("get local ").add(local.name.str);
		il.Emit(OpCodes.Ldloc, local.__builder);
	}

	internal void setLocal(Local local) {
		logger?.log().add("set local ").add(local.name.str);
		il.Emit(OpCodes.Stloc, local.__builder);
	}

	internal void doThrow() {
		logger?.log().add("throw");
		il.Emit(OpCodes.Throw);
	}

	static readonly Sym symEndTry = Sym.of("end try");
	internal Label beginTry() {
		logger?.log().add("begin try");
		return new Label(il.BeginExceptionBlock(), symEndTry);
	}

	internal void beginCatch(Type exceptionType) {
		logger?.log().add("catch ").add(exceptionType.Name);
		il.BeginCatchBlock(exceptionType);
	}

	internal void endTry() {
		logger?.log().add("end try");
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
