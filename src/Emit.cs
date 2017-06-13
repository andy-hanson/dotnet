using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

struct TypeBuilding {
	internal readonly TypeInfo info;
	internal readonly Op<Type> _type;
	internal Type type => _type.force;

	internal TypeBuilding(TypeInfo info) { this.info = info; this._type = Op<Type>.None; }
	internal TypeBuilding(TypeInfo info, Type type) { this.info = info; this._type = Op.Some(type); }
	internal TypeBuilding withType(Type type) { return new TypeBuilding(this.info, type); }
}

/**
Note: We *lazily* compile modules. But we must compile all of a module's imports before we compile it.
*/
class Emitter {
	readonly AssemblyName assemblyName = new AssemblyName("noze");
	readonly AssemblyBuilder assemblyBuilder;
	readonly Dictionary<Klass, TypeBuilding> typeInfos = new Dictionary<Klass, TypeBuilding>();
	//var mb = ab.DefineDynamicModule(aName.Name);

	internal Type getClass(Klass k) => typeInfos[k].type;

	internal Type toType(Ty ty) {
		var b = ty as BuiltinClass;
		if (b != null)
			return b.dotNetType;

		return typeInfos[(Klass) ty].type;
	}

	//This holds a mapping from Klass -> compiled class.
	internal Emitter() {
		assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
	}

	internal Type emitModule(Model.Module m) {
		if (typeInfos.TryGetValue(m.klass, out var b)) {
			return b.type;
		}

		foreach (var im in m.imports)
			emitModule(im);

		return doCompileModule(m);
	}

	Type doCompileModule(Model.Module m) {
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(m.name.str);
		var klass = m.klass;
		//TypeAttributes.Abstract;
		//TypeAttributes.Interface;
		var typeBuilder = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | TypeAttributes.Sealed);

		typeInfos[klass] = new TypeBuilding(typeBuilder.GetTypeInfo());

		//Fill in stuff!
		fillHead(typeBuilder, klass);
		fillMethods(typeBuilder, klass);

		var type = typeBuilder.CreateType();
		typeInfos[klass] = typeInfos[klass].withType(type);
		return type;
	}

	void fillHead(TypeBuilder tb, Klass klass) {
		var head = klass.head;

		var statik = head as Klass.Head.Static;
		if (statik != null)
			// Nothing to do for a static class.
			return;

		var slots = head as Klass.Head.Slots;
		if (slots != null) {
			foreach (var slot in slots.slots)
				tb.DefineField(slot.name.str, toType(slot.ty), FieldAttributes.Public);
			return;
		}

		throw TODO();
	}

	void fillMethods(TypeBuilder tb, Klass klass) {
		foreach (var member in klass.membersMap.values) {
			var method = (Method.MethodWithBody) member; //TODO

			var attr = MethodAttributes.Public;
			if (method.isStatic)
				attr |= MethodAttributes.Static;
			else
				attr |= MethodAttributes.Final;

			var mb = tb.DefineMethod(method.name.str, attr);

			mb.SetParameters(method.parameters.mapToArray(p => toType(p.ty)));
			mb.SetReturnType(toType(method.returnTy));


			var methIl = mb.GetILGenerator();
			var iw = new ILWriter(methIl);
			new ExprEmitter(this, iw).emitAny(method.body);
			iw.ret();
		}
	}
}

/*
static class Emit {
	/*internal static void writeBytecode(ModuleBuilder moduleBuilder, Klass klass, LineColumnGetter lineColumnGetter) {
		var tb = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public); //TODO: may need to store this with the class?
		//Or, just use a lookup.

		var ti = tb.CreateTypeInfo();
		//val bytes = classToBytecode(klass, lineColumnGetter);
		//set things on the klass

		//Create ourselves a class

	}* /

	static void foo(TypeBuilder tb, Klass klass) {
		var slots = ((Klass.Head.Slots) klass.head).slots;
		foreach (var slot in slots) {
			var fb = tb.DefineField(slot.name.str, slot.ty.toType(), FieldAttributes.Public);
		}

		foreach (var member in klass.membersMap.values) {
			var method = (MethodWithBody) member; //todo: other members

			var mb = tb.DefineMethod(method.name.str, MethodAttributes.Public, method.returnTy.toType(),
				method.parameters.MapToArray(p => p.ty.toType()));
			var methIl = mb.GetILGenerator();
			new ExprEmitter(methIl).emitAny(method.body);
		}

		//Fields, constructors, etc.
	}
}
*/

sealed class ILWriter {
	//readonly Arr.Builder<string> logs;
	readonly ILGenerator il;
	internal ILWriter(ILGenerator il) {
		this.il = il;
		//this.logs = Arr.builder<string>();
	}

	void log(string s) {
		//Console.WriteLine("  " + s);
		//logs.add(s);
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

	/** Remember to call markLabel! */
	internal Label label() {
		return il.DefineLabel();
	}

	internal void markLabel(Label l) {
		log($"{l}");
		il.MarkLabel(l);
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

	internal void getParameter(Method.Parameter p) {
		log($"get parameter {p.name}");
		il.Emit(ldargOperation(p.index));
	}

	OpCode ldargOperation(uint index) {
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

	//internal void construct(ConstructorInfo c) {
	//	il.Emit(OpCodes.Newobj, c);
	//}
}

sealed class ExprEmitter {
	readonly Emitter emitter;
	readonly ILWriter il;
	internal ExprEmitter(Emitter emitter, ILWriter il) { this.emitter = emitter; this.il = il; }

	internal void emitAny(Expr e) {
		var p = e as Expr.AccessParameter;
		if (p != null) {
			emitAccessParameter(p);
			return;
		}

		var lo = e as Expr.AccessLocal;
		if (lo != null) {
			emitAccessLocal(lo);
			return;
		}

		var l = e as Expr.Let;
		if (l != null) {
			emitLet(l);
			return;
		}

		var s = e as Expr.Seq;
		if (s != null) {
			emitSeq(s);
			return;
		}

		var li = e as Expr.Literal;
		if (li != null) {
			emitLiteral(li);
			return;
		}

		var sm = e as Expr.StaticMethodCall;
		if (sm != null) {
			emitStaticMethodCall(sm);
			return;
		}

		var g = e as Expr.GetSlot;
		if (g != null) {
			emitGetSlot(g);
			return;
		}

		var w = e as Expr.WhenTest;
		if (w != null) {
			emitWhenTest(w);
			return;
		}

		throw TODO();
	}

	void emitAccessParameter(Expr.AccessParameter p) {
		il.getParameter(p.param);
	}

	void emitAccessLocal(Expr.AccessLocal lo) {
		throw TODO();
	}

	void emitLet(Expr.Let l) {
		throw TODO();
	}

	void emitSeq(Expr.Seq s) {
		emitAny(s.action);
		emitAny(s.then);
		throw TODO();
	}

	static readonly FieldInfo fieldBoolTrue = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolTrue));
	static readonly FieldInfo fieldBoolFalse = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolFalse));
	static readonly MethodInfo staticMethodIntOf = typeof(Builtins.Int).GetMethod(nameof(Builtins.Int.of));
	static readonly MethodInfo staticMethodFloatOf = typeof(Builtins.Float).GetMethod(nameof(Builtins.Float.of));
	static readonly MethodInfo staticMethodStrOf = typeof(Builtins.Str).GetMethod(nameof(Builtins.Str.of));
	static readonly FieldInfo fieldVoidInstance = typeof(Builtins.Void).GetField(nameof(Builtins.Void.instance));
	void emitLiteral(Expr.Literal li) {
		var v = li.value;
		var vb = v as Expr.Literal.LiteralValue.Bool;
		if (vb != null) {
			il.loadStaticField(vb.value ? fieldBoolTrue : fieldBoolFalse);
			return;
		}

		var vi = v as Expr.Literal.LiteralValue.Int;
		if (vi != null) {
			il.constInt(vi.value);
			il.callStaticMethod(staticMethodIntOf);
			return;
		}

		var vf = v as Expr.Literal.LiteralValue.Float;
		if (vf != null) {
			il.constDouble(vf.value);
			il.callStaticMethod(staticMethodFloatOf);
			return;
		}

		var vs = v as Expr.Literal.LiteralValue.Str;
		if (vs != null) {
			il.constStr(vs.value);
			il.callStaticMethod(staticMethodStrOf);
			return;
		}

		var p = v as Expr.Literal.LiteralValue.Pass;
		if (p != null) {
			il.loadStaticField(fieldVoidInstance);
			return;
		}

		throw unreachable();
	}

	static readonly FieldInfo boolValue = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.value));
	void unpackBool() {
		il.getField(boolValue);
	}

	void emitWhenTest(Expr.WhenTest w) {
		/*
		test1:
		do test1
		ifnot: goto test2
			do test1 result
			goto end
		test2:
		do test2
		ifnot: goto elze
			do test2 result
			goto end
		elze:
			do elze result
			(already at end)
		end:
		...
		*/

		if (w.cases.length != 1) throw TODO(); //TODO
		var kase = w.cases[0];

		var elzeResultLabel = il.label();
		var end = il.label();

		emitAny(kase.test);
		unpackBool();
		il.goToIfFalse(elzeResultLabel);

		emitAny(kase.result);
		il.goTo(end);

		il.markLabel(elzeResultLabel);
		emitAny(w.elseResult);

		il.markLabel(end);
	}

	void emitStaticMethodCall(Expr.StaticMethodCall s) {
		throw TODO();
	}

	void emitGetSlot(Expr.GetSlot g) {
		throw TODO();
	}
}
