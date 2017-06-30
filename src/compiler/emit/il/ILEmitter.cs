using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

interface EmitterMaps {
	MethodInfo getMethodInfo(Method method);
	ConstructorInfo getConstructorInfo(Klass klass);
	FieldInfo getFieldInfo(Slot slot);
	Type toType(Ty ty);
}

sealed class EmitterMapsBuilder : EmitterMaps {
	// This will not be filled for a BuiltinMethod
	readonly Dictionary<Klass, TypeBuilding> typeInfos = new Dictionary<Klass, TypeBuilding>();
	internal readonly Dictionary<Method, MethodInfo> methodInfos = new Dictionary<Method, MethodInfo>();
	internal readonly Dictionary<Klass, ConstructorInfo> classToConstructor = new Dictionary<Klass, ConstructorInfo>();
	internal readonly Dictionary<Slot, FieldInfo> slotToField = new Dictionary<Slot, FieldInfo>();

	struct TypeBuilding {
		internal readonly TypeInfo info;
		readonly Op<Type> _type;
		internal Type type => _type.force;

		internal TypeBuilding(TypeInfo info) { this.info = info; this._type = Op<Type>.None; }
		internal TypeBuilding(TypeInfo info, Type type) { this.info = info; this._type = Op.Some(type); }
		internal TypeBuilding withType(Type type) => new TypeBuilding(this.info, type);
	}

	MethodInfo EmitterMaps.getMethodInfo(Method method) => methodInfos[method];
	ConstructorInfo EmitterMaps.getConstructorInfo(Klass klass) => classToConstructor[klass];
	FieldInfo EmitterMaps.getFieldInfo(Slot slot) => slotToField[slot];

	internal TypeInfo getTypeInfo(Klass k) => typeInfos[k].info;

	internal bool tryGetType(Klass k, out Type t) {
		var res = typeInfos.TryGetValue(k, out var tb);
		t = res ? tb.type : null;
		return res;
	}

	internal void beginTypeBuilding(Klass klass, TypeInfo info) =>
		typeInfos[klass] = new TypeBuilding(info);
	internal void finishTypeBuilding(Klass klass, Type type) =>
		typeInfos[klass] = typeInfos[klass].withType(type);

	Type EmitterMaps.toType(Ty ty) => toType(ty);
	internal Type toType(Ty ty) {
		switch (ty) {
			case BuiltinClass b:
				return b.dotNetType;
			case Klass k:
				return typeInfos[k].info;
			default:
				throw TODO();
		}
	}

	internal FieldInfo getSlotField(Slot slot) => slotToField[slot];

	internal MethodInfo toMethodInfo(Method method) =>
		method is Method.BuiltinMethod b ? b.methodInfo : methodInfos[method];
}

/**
Note: We *lazily* compile modules. But we must compile all of a module's imports before we compile it.
*/
sealed class ILEmitter {
	readonly AssemblyName assemblyName = new AssemblyName("noze");
	readonly AssemblyBuilder assemblyBuilder;
	readonly ModuleBuilder moduleBuilder;
	readonly EmitterMapsBuilder maps = new EmitterMapsBuilder();

	internal ILEmitter() {
		assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

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
		if (maps.tryGetType(m.klass, out var b))
			return b;

		foreach (var im in m.imports)
			emitModule(im);

		return doCompileModule(m);
	}

	Type doCompileModule(Model.Module m) {
		var klass = m.klass;
		//TypeAttributes.Abstract;
		//TypeAttributes.Interface;

		Type superClass;
		if (klass.supers.length != 0) {
			if (klass.supers.length > 1) throw TODO();
			var super = (Klass)klass.supers.only.superClass; //TODO: handle builtins
			superClass = maps.getTypeInfo(super);
		} else
			superClass = null; // No super class

		var typeBuilder = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head), superClass);

		maps.beginTypeBuilding(klass, typeBuilder.GetTypeInfo());

		fillHead(typeBuilder, klass);
		fillMethodsAndSupers(typeBuilder, klass);

		var type = typeBuilder.CreateTypeInfo();
		maps.finishTypeBuilding(klass, type);
		return type;
	}

	static TypeAttributes typeFlags(Klass.Head head) {
		switch (head) {
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
				// Nothing to do.
				return;

			case Klass.Head.Slots slots:
				var fields = slots.slots.map<FieldInfo>(slot => {
					var field = tb.DefineField(slot.name.str, maps.toType(slot.ty), FieldAttributes.Public);
					maps.slotToField.Add(slot, field);
					return field;
				});
				generateConstructor(tb, klass, fields);
				return;

			default:
				throw unreachable();
		}
	}

	void generateConstructor(TypeBuilder tb, Klass klass, Arr<FieldInfo> fields) {
		//Constructor can't call any other methods, so we generate it eagerly.
		var parameterTypes = fields.mapToArray(f => f.FieldType);
		var ctr = tb.DefineConstructor(MethodAttributes.Private, CallingConventions.Standard, parameterTypes);
		var il = new ILWriter(ctr);
		for (uint index = 0; index < fields.length; index++) {
			var field = fields[index];
			il.getThis();
			il.getParameter(index);
			il.setField(field);
		}
		il.ret();

		maps.classToConstructor[klass] = ctr;
	}

	void fillMethodsAndSupers(TypeBuilder tb, Klass klass) {
		foreach (var method in klass.methods)
			maps.methodInfos.Add(method, defineMethod(tb, method, methodAttributes(method)));

		foreach (var super in klass.supers) {
			foreach (var impl in super.impls) {
				var mb = defineMethod(tb, impl.implemented, implAttributes);
				ExprEmitter.emitMethodBody(maps, mb, impl.body);
			}
		}

		foreach (var method in klass.methods) {
			switch (method) {
				case Method.MethodWithBody mwb:
					var mb = (MethodBuilder)maps.methodInfos[mwb];
					ExprEmitter.emitMethodBody(maps, mb, mwb.body);
					break;
				case Method.AbstractMethod a:
					break;
				default:
					throw unreachable();
			}
		}
	}

	MethodBuilder defineMethod(TypeBuilder tb, Method m, MethodAttributes attrs) =>
		tb.DefineMethod(m.name.str, attrs, maps.toType(m.returnTy), m.parameters.mapToArray(p => maps.toType(p.ty)));

	const MethodAttributes implAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final;

	static MethodAttributes methodAttributes(Method method) {
		var attr = MethodAttributes.Public;
		switch (method) {
			case Method.MethodWithBody mwb:
				if (mwb.isStatic)
					attr |= MethodAttributes.Static;
				else
					attr |= MethodAttributes.Final;
				return attr;
			case Method.AbstractMethod a:
				return attr | MethodAttributes.Abstract | MethodAttributes.Virtual;
			default:
				throw unreachable();
		}
	}
}

sealed class ExprEmitter {
	readonly EmitterMaps maps;
	readonly ILWriter il;
	readonly Dictionary<Pattern.Single, ILWriter.Local> localToIl = new Dictionary<Pattern.Single, ILWriter.Local>();
	ExprEmitter(EmitterMaps maps, ILWriter il) { this.maps = maps; this.il = il; }

	internal static void emitMethodBody(EmitterMaps maps, MethodBuilder mb, Expr body) {
		var iw = new ILWriter(mb);
		new ExprEmitter(maps, iw).emitAny(body);
		iw.ret();
	}

	void emitAny(Expr expr) {
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
			case Expr.InstanceMethodCall m:
				emitInstanceMethodCall(m);
				return;
			case Expr.New n:
				emitNew(n);
				return;
			case Expr.GetSlot g:
				emitGetSlot(g);
				return;
			case Expr.GetMySlot g:
				emitGetMySlot(g);
				return;
			case Expr.WhenTest w:
				emitWhenTest(w);
				return;
			default:
				throw TODO();
		}
	}

	void emitAccessParameter(Expr.AccessParameter p) =>
		il.getParameter(p.param.index);

	void emitAccessLocal(Expr.AccessLocal lo) =>
		il.getLocal(localToIl[lo.local]);

	void emitLet(Expr.Let l) {
		emitAny(l.value);
		var assigned = (Pattern.Single)l.assigned; //TODO:patterns
		var loc = il.initLocal(maps.toType(assigned.ty));
		localToIl.Add(assigned, loc);
		emitAny(l.then);
		// Don't bother taking it out of the dictionary,
		// we've already checked that there are no illegal accesses.
	}

	void emitSeq(Expr.Seq s) {
		emitAny(s.action);
		// Will have pushed a Void onto the stack. Take it off.
		il.pop();
		emitAny(s.then);
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
		var kase = w.cases.only;

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
		emitArgs(s.args);
		il.callStaticMethod(maps.getMethodInfo(s.method));
	}

	void emitInstanceMethodCall(Expr.InstanceMethodCall m) {
		emitAny(m.target);
		emitArgs(m.args);
		il.callInstanceMethod(maps.getMethodInfo(m.method), m.method is Method.AbstractMethod);
	}

	void emitNew(Expr.New n) {
		var ctr = this.maps.getConstructorInfo(n.klass);
		emitArgs(n.args);
		il.callConstructor(ctr);
	}

	void emitArgs(Arr<Expr> args) {
		foreach (var arg in args)
			emitAny(arg);
	}

	void emitGetSlot(Expr.GetSlot g) {
		emitAny(g.target);
		il.getField(this.maps.getFieldInfo(g.slot));
	}

	void emitGetMySlot(Expr.GetMySlot g) {
		il.getThis();
		il.getField(this.maps.getFieldInfo(g.slot));
	}
}
