using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using Model;
using static Utils;

/**
Note: We *lazily* compile modules. But we must compile all of a module's imports before we compile it.
*/
sealed class ILEmitter {
	readonly AssemblyName assemblyName = new AssemblyName("noze");
	readonly AssemblyBuilder assemblyBuilder;
	readonly ModuleBuilder moduleBuilder;
	readonly EmitterMapsBuilder maps = new EmitterMapsBuilder();

	readonly Dictionary<Model.Module, string> logs;
	bool shouldLog => logs != null;

	internal ILEmitter(bool shouldLog) {
		assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
		if (shouldLog) this.logs = new Dictionary<Model.Module, string>();

		//??? https://stackoverflow.com/questions/17995945/how-to-debug-dynamically-generated-method
		/*var daType = typeof(DebuggableAttribute);
		var ctorInfo = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
		var caBuilder = new CustomAttributeBuilder(ctorInfo, new object[] {
			DebuggableAttribute.DebuggingModes.DisableOptimizations |
			DebuggableAttribute.DebuggingModes.Default
		});
		assemblyBuilder.SetCustomAttribute(caBuilder);*/
	}

	internal string getLogs(Model.Module module) => logs[module];

	internal Type emitModule(Model.Module module) {
		if (maps.tryGetType(module.klass, out var b))
			return b;

		foreach (var im in module.imports)
			emitModule(im);

		return doCompileModule(module);
	}

	Type doCompileModule(Model.Module module) {
		var lw = shouldLog ? Op.Some(new LogWriter()) : Op<LogWriter>.None;

		var klass = module.klass;

		Type superClass;
		if (klass.supers.length != 0) {
			if (klass.supers.length > 1) throw TODO();
			var super = (Klass)klass.supers.only.superClass; //TODO: handle builtins
			superClass = maps.getTypeInfo(super);
		} else
			superClass = null; // No super class

		var typeBuilder = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head), superClass);

		maps.beginTypeBuilding(klass, typeBuilder.GetTypeInfo());

		fillHead(typeBuilder, klass, lw);
		fillMethodsAndSupers(typeBuilder, klass, lw);

		if (lw.get(out var l)) logs[module] = l.finish();

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

	void fillHead(TypeBuilder tb, Klass klass, Op<LogWriter> lw) {
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
				var logger = Op<Logger>.None;
				if (lw.get(out var l)) {
					l.beginConstructor();
					logger = Op<Logger>.Some(l);
				}
				generateConstructor(tb, klass, fields, logger);
				if (lw.get(out var l2)) l.endConstructor();
				return;

			default:
				throw unreachable();
		}
	}

	void generateConstructor(TypeBuilder tb, Klass klass, Arr<FieldInfo> fields, Op<Logger> logger) {
		//Constructor can't call any other methods, so we generate it eagerly.
		var parameterTypes = fields.mapToArray(f => f.FieldType);
		var ctr = tb.DefineConstructor(MethodAttributes.Private, CallingConventions.Standard, parameterTypes);
		var il = new ILWriter(ctr, logger);
		for (uint index = 0; index < fields.length; index++) {
			var field = fields[index];
			il.getThis();
			il.getParameter(index);
			il.setField(field);
		}
		il.ret();

		maps.classToConstructor[klass] = ctr;
	}

	void fillMethodsAndSupers(TypeBuilder tb, Klass klass, Op<LogWriter> lw) {
		foreach (var method in klass.methods)
			maps.methodInfos.Add(method, defineMethod(tb, method, methodAttributes(method)));

		foreach (var super in klass.supers) {
			foreach (var impl in super.impls) {
				var mb = defineMethod(tb, impl.implemented, implAttributes);
				var logger = Op<Logger>.None;
				if (lw.get(out var l)) {
					l.beginImpl(impl);
					logger = Op.Some<Logger>(l);
				}
				ExprEmitter.emitMethodBody(maps, mb, impl.body, logger);
			}
		}

		foreach (var method in klass.methods) {
			switch (method) {
				case Method.MethodWithBody mwb:
					var mb = (MethodBuilder)maps.methodInfos[mwb];
					var logger = Op<Logger>.None;
					if (lw.get(out var l)) {
						l.beginMethod(mwb);
						logger = Op.Some<Logger>(l);
					}
					ExprEmitter.emitMethodBody(maps, mb, mwb.body, logger);
					if (lw.get(out var l2)) l2.endMethod();
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

	sealed class LogWriter : Logger {
		readonly StringBuilder sb = new StringBuilder();

		internal void beginImpl(Impl i) =>
			sb.Append($"impl {i.implemented.name}");

		internal void beginConstructor() =>
			sb.Append("constructor");
		internal void endConstructor() =>
			sb.Append("\n\n");

		internal void beginMethod(Method.MethodWithBody m) {
			var kw = m.isStatic ? "fun" : "def";
			sb.Append($"{kw} {m.name}");
		}
		internal void endMethod() =>
			sb.Append("\n\n");

		void Logger.log(string s) {
			sb.Append("\n\t");
			sb.Append(s);
		}

		internal string finish() =>
			sb.ToString();
	}

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

		MethodInfo EmitterMaps.getMethodInfo(Method method) =>
			method is Method.BuiltinMethod b ? b.methodInfo : methodInfos[method];
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
	}

	sealed class ExprEmitter {
		readonly EmitterMaps maps;
		readonly ILWriter il;
		readonly Dictionary<Pattern.Single, ILWriter.Local> localToIl = new Dictionary<Pattern.Single, ILWriter.Local>();
		ExprEmitter(EmitterMaps maps, ILWriter il) { this.maps = maps; this.il = il; }

		internal static void emitMethodBody(EmitterMaps maps, MethodBuilder mb, Expr body, Op<Logger> logger) {
			var iw = new ILWriter(mb, logger);
			new ExprEmitter(maps, iw).emitAny(body);
			iw.ret();
		}

		void emitAnyVoid(Expr e) {
			emitAny(e);
			// Will have pushed a Void onto the stack. Take it off.
			il.pop();
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
				case Expr.Try t:
					emitTry(t);
					return;
				case Expr.Assert a:
					emitAssert(a);
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
			initLocal((Pattern.Single)l.assigned); //TODO:patterns
			emitAny(l.then);
			// Don't bother taking it out of the dictionary,
			// we've already checked that there are no illegal accesses.
		}

		void initLocal(Pattern.Single pattern) => initLocal(pattern, maps.toType(pattern.ty));
		void initLocal(Pattern.Single pattern, Type type) {
			var local = il.initLocal(type, pattern.name);
			localToIl.Add(pattern, local);
		}

		void emitSeq(Expr.Seq s) {
			emitAnyVoid(s.action);
			emitAny(s.then);
		}

		static readonly FieldInfo fieldBoolTrue = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolTrue));
		static readonly FieldInfo fieldBoolFalse = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolFalse));
		static readonly MethodInfo staticMethodIntOf = typeof(Builtins.Int).GetMethod(nameof(Builtins.Int.of));
		static readonly MethodInfo staticMethodFloatOf = typeof(Builtins.Float).GetMethod(nameof(Builtins.Float.of));
		static readonly MethodInfo staticMethodStrOf = typeof(Builtins.String).GetMethod(nameof(Builtins.String.of));
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
					emitVoid();
					return;
				default:
					throw unreachable();
			}
		}

		//TODO: don't do this if not necessary.
		void emitVoid() {
			il.loadStaticField(fieldVoidInstance);
		}

		static readonly FieldInfo boolValue = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.value));
		void unpackBool() {
			il.getField(boolValue);
		}

		static readonly Sym symElse = Sym.of("else");
		static readonly Sym symEndWhen = Sym.of("endWhen");
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

			var elzeResultLabel = il.label(symElse);
			var end = il.label(symEndWhen);

			emitAny(kase.test);
			unpackBool();
			il.goToIfFalse(elzeResultLabel);

			emitAny(kase.result);
			il.goTo(end);

			il.markLabel(elzeResultLabel);
			emitAny(w.elseResult);

			il.markLabel(end);
		}

		static readonly Sym symTryResult = Sym.of("tryResult");
		void emitTry(Expr.Try t) {
			//var failed = il.DefineLabel();
			var res = il.declareLocal(maps.toType(t.ty), symTryResult);

			// try
			var end = il.beginTry();
			emitAny(t.do_);
			il.setLocal(res);
			//il.Emit(OpCodes.Leave, end);

			// catch
			if (t.catch_.get(out var c)) {
				var catch_ = t.catch_.force; //TODO: handle missing catch
				var exceptionType = maps.toType(catch_.exceptionTy);
				il.beginCatch(exceptionType);
				// Catch block starts with exception on the stack. Put it in a local.
				initLocal(catch_.caught, exceptionType);
				emitAny(catch_.then);
				this.il.setLocal(res);
			}

			if (t.finally_.get(out var f)) {
				throw TODO();
				//il.???
				//emitAnyVoid(finally_);
			}

			il.endTry();

			this.il.getLocal(res);
		}

		static readonly Sym symEndAssert = Sym.of("endAssert");
		void emitAssert(Expr.Assert a) {
			var end = il.label(symEndAssert);

			emitAny(a.asserted);
			unpackBool();
			il.goToIfTrue(end);

			var exceptionType = typeof(Builtins.AssertionException);
			var ctr = exceptionType.GetConstructor(new Type[] {});
			il.callConstructor(ctr);
			il.@throw();

			il.markLabel(end);

			emitVoid();
		}

		void emitStaticMethodCall(Expr.StaticMethodCall s) {
			emitArgs(s.args);
			il.callStaticMethod(maps.getMethodInfo(s.method));
		}

		void emitInstanceMethodCall(Expr.InstanceMethodCall m) {
			emitAny(m.target);
			emitArgs(m.args);
			il.callInstanceMethod(maps.getMethodInfo(m.method), m.method.isAbstract);
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
}
