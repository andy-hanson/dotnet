using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

sealed class ILExprEmitter {
	readonly EmitterMaps maps;
	readonly ILWriter il;
	readonly MethodInfo currentMethod;
	readonly Dict.Builder<Pattern.Single, ILWriter.Local> localToIl = Dict.builder<Pattern.Single, ILWriter.Local>();
	ILExprEmitter(EmitterMaps maps, ILWriter il, MethodInfo currentMethod) {
		this.maps = maps;
		this.il = il;
		this.currentMethod = currentMethod;
	}

	internal static void emitMethodBody(EmitterMaps maps, MethodBuilder mb, Expr body, /*nullable*/ InstructionLogger logger) {
		var iw = new ILWriter(mb, logger);
		new ILExprEmitter(maps, iw, mb).emitAny(body);
		iw.ret();
	}

	void emitAnyVoid(Expr e) {
		emitAny(e);
		// Will have pushed a Void onto the stack. Take it off.
		il.pop();
	}

	void emitAny(Expr expr) {
		switch (expr) {
			case AccessParameter p:
				emitAccessParameter(p);
				break;
			case AccessLocal lo:
				emitAccessLocal(lo);
				return;
			case Let l:
				emitLet(l);
				return;
			case Seq s:
				emitSeq(s);
				return;
			case Literal li:
				emitLiteral(li);
				return;
			case StaticMethodCall sm:
				emitStaticMethodCall(sm);
				return;
			case InstanceMethodCall m:
				emitInstanceMethodCall(m);
				return;
			case MyInstanceMethodCall my:
				emitMyInstanceMethodCall(my);
				return;
			case New n:
				emitNew(n);
				return;
			case Recur r:
				emitRecur(r);
				return;
			case GetSlot g:
				emitGetSlot(g);
				return;
			case GetMySlot g:
				emitGetMySlot(g);
				return;
			case SetSlot s:
				emitSetSlot(s);
				return;
			case IfElse i:
				emitIfElse(i);
				return;
			case WhenTest w:
				emitWhenTest(w);
				return;
			case Try t:
				emitTry(t);
				return;
			case Assert a:
				emitAssert(a);
				return;
			default:
				throw TODO();
		}
	}

	void emitAccessParameter(AccessParameter p) =>
		il.getParameter(p.param.index);

	void emitAccessLocal(AccessLocal lo) =>
		il.getLocal(localToIl[lo.local]);

	void emitLet(Let l) {
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

	void emitSeq(Seq s) {
		emitAnyVoid(s.action);
		emitAny(s.then);
	}

	static readonly FieldInfo fieldBoolTrue = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolTrue));
	static readonly FieldInfo fieldBoolFalse = typeof(Builtins.Bool).GetField(nameof(Builtins.Bool.boolFalse));
	static readonly MethodInfo staticMethodNatOf = typeof(Builtins.Nat).GetMethod(nameof(Builtins.Nat.of));
	static readonly MethodInfo staticMethodIntOf = typeof(Builtins.Int).GetMethod(nameof(Builtins.Int.of));
	static readonly MethodInfo staticMethodFloatOf = typeof(Builtins.Real).GetMethod(nameof(Builtins.Real.of));
	static readonly MethodInfo staticMethodStrOf = typeof(Builtins.String).GetMethod(nameof(Builtins.String.of));
	static readonly FieldInfo fieldVoidInstance = typeof(Builtins.Void).GetField(nameof(Builtins.Void.instance));
	void emitLiteral(Literal li) {
		switch (li.value) {
			case LiteralValue.Bool vb:
				il.loadStaticField(vb.value ? fieldBoolTrue : fieldBoolFalse);
				return;
			case LiteralValue.Nat vn:
				il.constUint(vn.value);
				il.callNonVirtual(staticMethodNatOf);
				return;
			case LiteralValue.Int vi:
				il.constInt(vi.value);
				il.callNonVirtual(staticMethodIntOf);
				return;
			case LiteralValue.Real vf:
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
	static readonly Sym symEndIf = Sym.of("endIf");
	static readonly Sym symEndWhen = Sym.of("endWhen");
	static readonly List<Sym> caseSyms = new List<Sym>();
	static Sym caseSym(uint idx) {
		while (caseSyms.Count <= idx)
			caseSyms.Add(Sym.of("case" + caseSyms.Count));
		return caseSyms[signed(idx)];
	}

	void emitIfElse(IfElse i) {
		/*
		{condition}
		if not, goto elze
		{then}
		goto end
		elze:
		{else}
		end:
		*/

		var elze = il.label(symElse);
		var end = il.label(symEndIf);

		emitAny(i.test);
		unpackBool();
		il.goToIfFalse(elze);
		emitAny(i.then);
		il.goTo(end);
		il.markLabel(elze);
		emitAny(i.@else);
		il.markLabel(end);
	}

	void emitWhenTest(WhenTest w) {
		/*
		{test0}
		ifnot: goto case1
		{result0}
		goto end
		case1:
		{test1}
		ifnot: goto elze
		{result1}
		goto end
		...
		elze:
		{elseResult}
		end:
		*/

		// At index i, stores the label for case i + 1.
		var nextCaseLabels = Arr.buildWithIndex(w.cases.length - 1, i => il.label(caseSym(i + 1)));

		var elseResultLabel = il.label(symElse);
		var end = il.label(symEndWhen);

		for (uint i = 0; i < w.cases.length; i++) {
			var kase = w.cases[i];
			if (i != 0)
				il.markLabel(nextCaseLabels[i - 1]);

			emitAny(kase.test);
			unpackBool();
			il.goToIfFalse(i == w.cases.length - 1 ? elseResultLabel : nextCaseLabels[i]);
			emitAny(kase.result);
			il.goTo(end);
		}

		il.markLabel(elseResultLabel);
		emitAny(w.elseResult);

		il.markLabel(end);
	}

	static readonly Sym symTryResult = Sym.of("tryResult");
	void emitTry(Try t) {
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
	void emitAssert(Assert a) {
		var end = il.label(symEndAssert);

		emitAny(a.asserted);
		unpackBool();
		il.goToIfTrue(end);

		var exceptionType = typeof(Builtins.Assertion_Exception);
		var ctr = exceptionType.GetConstructor(new Type[] {});
		il.callConstructor(ctr);
		il.doThrow();

		il.markLabel(end);

		emitVoid();
	}

	void emitStaticMethodCall(StaticMethodCall s) {
		emitArgs(s.args);
		il.callNonVirtual(maps.getMethodInfo(s.method));
	}

	void emitInstanceMethodCall(InstanceMethodCall m) {
		emitAny(m.target);
		emitArgs(m.args);
		/*
		This may actually call a static method in these cases:
		An abstract class is implemented as an IL interface, so its "instance" methods are emitted as static methods instead.
		If a builtin is implemented by a struct, its "instance" methods are emitted as static methods to avoid having to pass by ref.
		*/
		il.call(maps.getMethodInfo(m.method), isVirtual: m.method.isAbstract);
	}

	void emitMyInstanceMethodCall(MyInstanceMethodCall m) {
		il.getThis();
		emitArgs(m.args);
		//TODO: we still might know an exact type... so wouldn't need a virtual call...
		il.call(maps.getMethodInfo(m.method), isVirtual: m.method.isAbstract);
	}

	void emitNew(New n) {
		var ctr = this.maps.getConstructorInfo(n.klass);
		emitArgs(n.args);
		il.callConstructor(ctr);
	}

	void emitRecur(Recur r) {
		emitArgs(r.args);
		il.tailcallNonVirtual(currentMethod);
	}

	void emitArgs(Arr<Expr> args) {
		foreach (var arg in args)
			emitAny(arg);
	}

	void emitGetSlot(GetSlot g) {
		emitAny(g.target);
		il.getField(this.maps.getFieldInfo(g.slot));
	}

	void emitGetMySlot(GetMySlot g) {
		il.getThis();
		il.getField(this.maps.getFieldInfo(g.slot));
	}

	void emitSetSlot(SetSlot s) {
		il.getThis();
		emitAny(s.value);
		il.setField(this.maps.getFieldInfo(s.slot));
		emitVoid();
	}
}
