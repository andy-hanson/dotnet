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

	internal void pop() {
		logger?.log(nameof(pop));
		il.Emit(OpCodes.Pop);
	}

	internal void ret() {
		logger?.log("return");
		il.Emit(OpCodes.Ret);
	}

	internal void constInt(int i) {
		logger?.log($"const {i}");
		il.Emit(OpCodes.Ldc_I4, i);
	}

	internal void constDouble(double d) {
		logger?.log($"const {d}");
		il.Emit(OpCodes.Ldc_R8, d);
	}

	internal void constString(string s) {
		logger?.log($"const \"{s}\"");
		il.Emit(OpCodes.Ldstr, s);
	}

	internal void loadStaticField(FieldInfo field) {
		logger?.log($"load static field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldsfld, field);
	}

	internal void callStaticMethod(MethodInfo method) {
		logger?.log($"call static {method.DeclaringType.Name}.{method.Name}");
		assert(method.IsStatic);
		// Last arg only matters if this is varargs.
		il.EmitCall(OpCodes.Call, method, null);
	}

	internal void callConstructor(ConstructorInfo ctr) {
		logger?.log($"new {ctr.DeclaringType.Name}");
		il.Emit(OpCodes.Newobj, ctr);
	}

	/** Remember to call markLabel! */
	internal Label label(Sym name) =>
		new Label(il.DefineLabel(), name);

	internal void markLabel(Label l) {
		logger?.log($"{l.name}:");
		il.MarkLabel(l.__inner);
	}

	internal void getThis() {
		logger?.log("this");
		assert(!isStatic);
		il.Emit(OpCodes.Ldarg_0);
	}

	internal void setField(FieldInfo field) {
		logger?.log($"set instance field {field.Name}");
		il.Emit(OpCodes.Stfld, field);
	}

	internal void getField(FieldInfo field) {
		logger?.log($"get instance field {field.DeclaringType.Name}.{field.Name}");
		il.Emit(OpCodes.Ldfld, field);
	}

	internal void goToIfFalse(Label lbl) {
		logger?.log($"goto if false: {lbl.name}");
		il.Emit(OpCodes.Brfalse, lbl.__inner);
	}

	internal void goToIfTrue(Label lbl) {
		logger?.log($"goto if true: {lbl.name}");
		il.Emit(OpCodes.Brtrue, lbl.__inner);
	}

	internal void goTo(Label l) {
		logger?.log($"goto {l.name}");
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

	internal void callInstanceMethod(MethodInfo method, bool isVirtual) {
		logger?.log($"call instance {method.DeclaringType.Name}.{method.Name}");
		if (isVirtual) assert(method.IsVirtual); // Note that we *can* call a virtual method statically.
		var opcode = isVirtual ? OpCodes.Callvirt : OpCodes.Call;
		il.Emit(opcode, method);
	}

	internal Local declareLocal(Type type, Sym name) {
		logger?.log($"declare local {name}");
		return new Local(il.DeclareLocal(type), name);
	}

	/** Should be called after pushing the locals' initial value on the stack. */
	internal Local initLocal(Type type, Sym name) {
		var local = declareLocal(type, name);
		setLocal(local);
		return local;
	}

	internal void getLocal(Local local) {
		logger?.log($"get local {local.name}");
		il.Emit(OpCodes.Ldloc, local.__builder);
	}

	internal void setLocal(Local local) {
		logger?.log($"set local {local.name}");
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
