using System;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

sealed class ILExprEmitter {
	readonly EmitterMaps maps;
	readonly ILWriter il;
	readonly Dict.Builder<Pattern.Single, ILWriter.Local> localToIl = Dict.builder<Pattern.Single, ILWriter.Local>();
	ILExprEmitter(EmitterMaps maps, ILWriter il) { this.maps = maps; this.il = il; }

	internal static void emitMethodBody(EmitterMaps maps, MethodBuilder mb, Expr body, /*nullable*/ Logger logger) {
		var iw = new ILWriter(mb, logger);
		new ILExprEmitter(maps, iw).emitAny(body);
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
			case Expr.MyInstanceMethodCall my:
				emitMyInstanceMethodCall(my);
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
		localToIl.add(pattern, local);
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
			case LiteralValue.Bool vb:
				il.loadStaticField(vb.value ? fieldBoolTrue : fieldBoolFalse);
				return;
			case LiteralValue.Int vi:
				il.constInt(vi.value);
				il.callNonVirtual(staticMethodIntOf);
				return;
			case LiteralValue.Float vf:
				il.constDouble(vf.value);
				il.callNonVirtual(staticMethodFloatOf);
				return;
			case LiteralValue.String vs:
				il.constString(vs.value);
				il.callNonVirtual(staticMethodStrOf);
				return;
			case LiteralValue.Pass p:
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
		il.beginTry();
		emitAny(t._do);
		il.setLocal(res);

		// catch
		if (t._catch.get(out var c)) {
			var catch_ = t._catch.force; //TODO: handle missing catch
			var exceptionType = maps.toType(catch_.exceptionTy);
			il.beginCatch(exceptionType);
			// Catch block starts with exception on the stack. Put it in a local.
			initLocal(catch_.caught, exceptionType);
			emitAny(catch_.then);
			this.il.setLocal(res);
		}

		if (t._finally.get(out var f)) {
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
		il.doThrow();

		il.markLabel(end);

		emitVoid();
	}

	void emitStaticMethodCall(Expr.StaticMethodCall s) {
		emitArgs(s.args);
		il.callNonVirtual(maps.getMethodInfo(s.method));
	}

	void emitInstanceMethodCall(Expr.InstanceMethodCall m) {
		emitAny(m.target);
		emitArgs(m.args);
		// This will work for either a regular instance method call or for an interface 'instance' with static 'this'.
		il.call(maps.getMethodInfo(m.method), isVirtual: m.method.isAbstract);
	}

	void emitMyInstanceMethodCall(Expr.MyInstanceMethodCall m) {
		il.getThis();
		emitArgs(m.args);
		//TODO: we still might know an exact type... so wouldn't need a virtual call...
		il.call(maps.getMethodInfo(m.method), isVirtual: m.method.isAbstract);
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
