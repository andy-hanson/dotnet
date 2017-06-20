using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

struct TypeBuilding {
	readonly TypeInfo info;
	readonly Op<Type> _type;
	internal Type type => _type.force;

	internal TypeBuilding(TypeInfo info) { this.info = info; this._type = Op<Type>.None; }
	internal TypeBuilding(TypeInfo info, Type type) { this.info = info; this._type = Op.Some(type); }
	internal TypeBuilding withType(Type type) { return new TypeBuilding(this.info, type); }
}

/**
Note: We *lazily* compile modules. But we must compile all of a module's imports before we compile it.
*/
class ILEmitter {
	readonly AssemblyName assemblyName = new AssemblyName("noze");
	readonly Dictionary<Klass, TypeBuilding> typeInfos = new Dictionary<Klass, TypeBuilding>();
	readonly AssemblyBuilder assemblyBuilder;
	readonly ModuleBuilder moduleBuilder;

	internal Type getClass(Klass k) => typeInfos[k].type;

	internal Type toType(Ty ty) {
		switch (ty) {
			case BuiltinClass b:
				return b.dotNetType;
			case Klass k:
				return typeInfos[k].type;
			default:
				throw TODO();
		}
	}

	internal ILEmitter() {
		assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		moduleBuilder = assemblyBuilder.DefineDynamicModule("noze");

		//??? https://stackoverflow.com/questions/17995945/how-to-debug-dynamically-generated-method
		/*var daType = typeof(DebuggableAttribute);
		var ctorInfo = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
		var caBuilder = new CustomAttributeBuilder(ctorInfo, new object[] {
  			DebuggableAttribute.DebuggingModes.DisableOptimizations |
			DebuggableAttribute.DebuggingModes.Default
		});
		assemblyBuilder.SetCustomAttribute(caBuilder);*/
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
		var klass = m.klass;
		//TypeAttributes.Abstract;
		//TypeAttributes.Interface;
		var typeBuilder = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head));

		typeInfos[klass] = new TypeBuilding(typeBuilder.GetTypeInfo());

		fillHead(typeBuilder, klass);
		fillMethods(typeBuilder, klass);

		var type = typeBuilder.CreateTypeInfo();
		typeInfos[klass] = typeInfos[klass].withType(type);
		return type;
	}

	TypeAttributes typeFlags(Klass.Head head) {
		switch (head ){
			case Klass.Head.Static s:
				return TypeAttributes.Sealed;
			case Klass.Head.Abstract a:
				return TypeAttributes.Abstract;
			case Klass.Head.Slots s:
				return TypeAttributes.Sealed;
			default:
				throw unreachable();
		}
	}

	void fillHead(TypeBuilder tb, Klass klass) {
		switch (klass.head) {
			case Klass.Head.Static s:
				// Nothing to do for a static class.
				return;

			case Klass.Head.Abstract a:
				throw TODO();

			case Klass.Head.Slots slots:
				foreach (var slot in slots.slots)
					tb.DefineField(slot.name.str, toType(slot.ty), FieldAttributes.Public);
				return;

			default:
				throw unreachable();
		}
	}

	void fillMethods(TypeBuilder tb, Klass klass) {
		foreach (var method in klass.methods) {
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
	readonly ILEmitter emitter;
	readonly ILWriter il;
	internal ExprEmitter(ILEmitter emitter, ILWriter il) { this.emitter = emitter; this.il = il; }

	internal void emitAny(Expr expr) {
		switch (expr) {
			case Expr.AccessParameter p:
				emitAccessParameter(p);
				break;
			case Expr.AccessLocal lo:
				emitAccessLocal(lo);
				return;
			case Expr.Let l:
				emitLet(l);
				return;
			case Expr.Seq s:
				emitSeq(s);
				return;
			case Expr.Literal li:
				emitLiteral(li);
				return;
			case Expr.StaticMethodCall sm:
				emitStaticMethodCall(sm);
				return;
			case Expr.GetSlot g:
				emitGetSlot(g);
				return;
			case Expr.WhenTest w:
				emitWhenTest(w);
				return;
			default:
				throw TODO();
		}
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
		switch (li.value) {
			case Expr.Literal.LiteralValue.Bool vb:
				il.loadStaticField(vb.value ? fieldBoolTrue : fieldBoolFalse);
				return;
			case Expr.Literal.LiteralValue.Int vi:
				il.constInt(vi.value);
				il.callStaticMethod(staticMethodIntOf);
				return;
			case Expr.Literal.LiteralValue.Float vf:
				il.constDouble(vf.value);
				il.callStaticMethod(staticMethodFloatOf);
				return;
			case Expr.Literal.LiteralValue.Str vs:
				il.constStr(vs.value);
				il.callStaticMethod(staticMethodStrOf);
				return;
			case Expr.Literal.LiteralValue.Pass p:
				il.loadStaticField(fieldVoidInstance);
				return;
			default:
				throw unreachable();
		}
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