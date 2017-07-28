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

	void emitAccessParameter(AccessParameter p) {
		var (_, param) = p;
		il.getParameter(param.index);
	}

	void emitAccessLocal(AccessLocal lo) {
		var (_, local) = lo;
		il.getLocal(localToIl[local]);
	}

	void emitLet(Let let) {
		var (_, assigned, value, then) = let;
		emitAny(value);
		initLocal((Pattern.Single)assigned); //TODO:patterns
		emitAny(then);
		// Don't bother taking it out of the dictionary,
		// we've already checked that there are no illegal accesses.
	}

	void initLocal(Pattern.Single pattern) => initLocal(pattern, maps.toType(pattern.ty));
	void initLocal(Pattern.Single pattern, Type type) {
		var local = il.initLocal(type, pattern.name);
		localToIl.add(pattern, local);
	}

	void emitSeq(Seq s) {
		var (_, action, then) = s;
		emitAnyVoid(action);
		emitAny(then);
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

	void emitIfElse(IfElse ifElse) {
		/*
		{condition}
		if not, goto elze
		{then}
		goto end
		elze:
		{else}
		end:
		*/
		var (_, test, then, @else) = ifElse;

		var elze = il.label(symElse);
		var end = il.label(symEndIf);

		emitAny(test);
		unpackBool();
		il.goToIfFalse(elze);
		emitAny(then);
		il.goTo(end);
		il.markLabel(elze);
		emitAny(@else);
		il.markLabel(end);
	}

	void emitWhenTest(WhenTest whenTest) {
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
		var (_, cases, elseResult) = whenTest;

		// At index i, stores the label for case i + 1.
		var nextCaseLabels = Arr.buildWithIndex(cases.length - 1, i => il.label(caseSym(i + 1)));

		var elseResultLabel = il.label(symElse);
		var end = il.label(symEndWhen);

		for (uint i = 0; i < cases.length; i++) {
			var kase = cases[i];
			if (i != 0)
				il.markLabel(nextCaseLabels[i - 1]);

			emitAny(kase.test);
			unpackBool();
			il.goToIfFalse(i == cases.length - 1 ? elseResultLabel : nextCaseLabels[i]);
			emitAny(kase.result);
			il.goTo(end);
		}

		il.markLabel(elseResultLabel);
		emitAny(elseResult);

		il.markLabel(end);
	}

	static readonly Sym symTryResult = Sym.of("tryResult");
	void emitTry(Try @try) {
		var (_, @do, @catch, @finally) = @try;

		//var failed = il.DefineLabel();
		var res = il.declareLocal(maps.toType(@try.ty), symTryResult);

		// try
		il.beginTry();
		emitAny(@do);
		il.setLocal(res);

		// catch
		if (@catch.get(out var catch_)) {
			var exceptionType = maps.toType(catch_.exceptionTy);
			il.beginCatch(exceptionType);
			// Catch block starts with exception on the stack. Put it in a local.
			initLocal(catch_.caught, exceptionType);
			emitAny(catch_.then);
			this.il.setLocal(res);
		}

		if (@finally.get(out var f)) {
			throw TODO();
			//il.???
			//emitAnyVoid(finally_);
		}

		il.endTry();

		this.il.getLocal(res);
	}

	static readonly Sym symEndAssert = Sym.of("endAssert");
	void emitAssert(Assert a) {
		var (_, asserted) = a;
		var end = il.label(symEndAssert);

		emitAny(asserted);
		unpackBool();
		il.goToIfTrue(end);

		var exceptionType = typeof(Builtins.Assertion_Exception);
		var ctr = exceptionType.GetConstructor(new Type[] {});
		il.callConstructor(ctr);
		il.doThrow();

		il.markLabel(end);

		emitVoid();
	}

	void emitStaticMethodCall(StaticMethodCall stat) {
		var (_, method, args) = stat;
		emitArgs(args);
		il.callNonVirtual(maps.getMethodInfo(method));
	}

	void emitInstanceMethodCall(InstanceMethodCall instance) {
		var (_, target, method, args) = instance;
		emitAny(target);
		emitArgs(args);
		/*
		This may actually call a static method in these cases:
		An abstract class is implemented as an IL interface, so its "instance" methods are emitted as static methods instead.
		If a builtin is implemented by a struct, its "instance" methods are emitted as static methods to avoid having to pass by ref.
		*/
		il.call(maps.getMethodInfo(method), isVirtual: method.isAbstract);
	}

	void emitMyInstanceMethodCall(MyInstanceMethodCall myInstance) {
		var (_, method, args) = myInstance;
		il.getThis();
		emitArgs(args);
		//TODO: we still might know an exact type... so wouldn't need a virtual call...
		il.call(maps.getMethodInfo(method), isVirtual: method.isAbstract);
	}

	void emitNew(New n) {
		var (_, slots, args) = n;
		var ctr = this.maps.getConstructorInfo(slots.klass);
		emitArgs(n.args);
		il.callConstructor(ctr);
	}

	void emitRecur(Recur r) {
		var (loc, _, args) = r;
		emitArgs(args);
		il.tailcallNonVirtual(currentMethod);
	}

	void emitArgs(Arr<Expr> args) {
		foreach (var arg in args)
			emitAny(arg);
	}

	void emitGetSlot(GetSlot g) {
		var (_, target, slot) = g;
		emitAny(target);
		il.getField(this.maps.getFieldInfo(slot));
	}

	void emitGetMySlot(GetMySlot g) {
		var (_, slot) = g;
		il.getThis();
		il.getField(this.maps.getFieldInfo(slot));
	}

	void emitSetSlot(SetSlot s) {
		var (_, slot, value) = s;
		il.getThis();
		emitAny(value);
		il.setField(this.maps.getFieldInfo(slot));
		emitVoid();
	}
}
