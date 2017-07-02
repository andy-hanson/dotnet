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
sealed class ILWriter {
	readonly ILGenerator il;
	readonly bool isStatic;
	readonly Op<Logger> logger;

	bool shouldLog => logger.has;
	void log(string s) => logger.force.log(s);

	internal ILWriter(MethodBuilder mb) : this(mb, Op<Logger>.None) {}
	internal ILWriter(MethodBuilder mb, Op<Logger> logger) {
		il = mb.GetILGenerator();
		isStatic = mb.IsStatic;
		this.logger = logger;
	}

	internal ILWriter(ConstructorBuilder cb) : this(cb, Op<Logger>.None) {}
	internal ILWriter(ConstructorBuilder cb, Op<Logger> logger) {
		il = cb.GetILGenerator();
		isStatic = false;
		this.logger = logger;
	}

	ILWriter(ILGenerator il, bool isStatic) {
		this.il = il;
		this.isStatic = isStatic;
	}

	internal void pop() {
		if (shouldLog) log("pop");
		il.Emit(OpCodes.Pop);
	}

	internal void ret() {
		if (shouldLog) log("return");
		il.Emit(OpCodes.Ret);
	}

	internal void constInt(int i) {
		if (shouldLog) log($"const {i}");
		il.Emit(OpCodes.Ldc_I4, i);
	}

	internal void constDouble(double d) {
		if (shouldLog) log($"const {d}");
		il.Emit(OpCodes.Ldc_R8, d);
	}

	internal void constStr(string s) {
		if (shouldLog) log($"const \"{s}\"");
		il.Emit(OpCodes.Ldstr, s);
	}

	internal void loadStaticField(FieldInfo field) {
		if (shouldLog) log($"load static field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldsfld, field);
	}

	internal void callStaticMethod(MethodInfo method) {
		if (shouldLog) log($"call static {method.DeclaringType.Name}.{method.Name}");
		assert(method.IsStatic);
		// Last arg only matters if this is varargs.
		il.EmitCall(OpCodes.Call, method, null);
	}

	// "new"
	internal void callConstructor(ConstructorInfo ctr) {
		if (shouldLog) log($"new {ctr.DeclaringType.Name}");
		il.Emit(OpCodes.Newobj, ctr);
	}

	/** Remember to call markLabel! */
	internal Label label(Sym name) =>
		new Label(il.DefineLabel(), name);

	internal void markLabel(Label l) {
		if (shouldLog) log($"{l.name}:");
		il.MarkLabel(l.__inner);
	}

	internal void getThis() {
		if (shouldLog) log("this");
		assert(!isStatic);
		il.Emit(OpCodes.Ldarg_0);
	}

	internal void setField(FieldInfo field) {
		if (shouldLog) log($"set instance field {field.Name}");
		il.Emit(OpCodes.Stfld, field);
	}

	internal void getField(FieldInfo field) {
		if (shouldLog) log($"get instance field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldfld, field);
	}

	internal void goToIfFalse(Label lbl) {
		if (shouldLog) log($"goto if false: {lbl.name}");
		il.Emit(OpCodes.Brfalse, lbl.__inner);
	}

	internal void goToIfTrue(Label lbl) {
		if (shouldLog) log($"goto if true: {lbl.name}");
		il.Emit(OpCodes.Brtrue, lbl.__inner);
	}

	internal void goTo(Label l) {
		if (shouldLog) log($"goto {l.name}");
		il.Emit(OpCodes.Br, l.__inner);
	}

	internal void getParameter(uint index) {
		if (shouldLog) log($"get parameter {index}");
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

	internal void callInstanceMethod(MethodInfo method, bool isVirtual) {
		if (shouldLog) log($"call instance {method.DeclaringType.Name}.{method.Name}");
		if (isVirtual) assert(method.IsVirtual); // Note that we *can* call a virtual method statically.
		var opcode = isVirtual ? OpCodes.Callvirt : OpCodes.Call;
		il.Emit(opcode, method);
	}

	internal Local declareLocal(Type type, Sym name) {
		if (shouldLog) log($"declare local {name}");
		return new Local(il.DeclareLocal(type), name);
	}

	/** Should be called after pushing the locals' initial value on the stack. */
	internal Local initLocal(Type type, Sym name) {
		var local = declareLocal(type, name);
		setLocal(local);
		return local;
	}

	internal void getLocal(Local local) {
		if (shouldLog) log($"get local {local.name}");
		il.Emit(OpCodes.Ldloc, local.__builder);
	}

	internal void setLocal(Local local) {
		if (shouldLog) log($"set local {local.name}");
		il.Emit(OpCodes.Stloc, local.__builder);
	}

	internal void @throw() {
		if (shouldLog) log("throw");
		il.Emit(OpCodes.Throw);
	}

	static readonly Sym symEndTry = Sym.of("end try");
	internal Label beginTry() {
		if (shouldLog) log("begin try");
		return new Label(il.BeginExceptionBlock(), symEndTry);
	}

	internal void beginCatch(Type exceptionType) {
		if (shouldLog) log($"catch {exceptionType}");
		il.BeginCatchBlock(exceptionType);
	}

	internal void endTry() {
		if (shouldLog) log("end try");
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
