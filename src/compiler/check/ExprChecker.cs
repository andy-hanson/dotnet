using System.Collections.Generic;

using Model;
using static Utils;

class ExprChecker {
	internal static Expr checkMethod(BaseScope baseScope, MethodOrImpl methodOrImpl, bool isStatic, Method implemented, Ast.Expr body) =>
		new ExprChecker(baseScope, methodOrImpl, isStatic, implemented.selfEffect, implemented.parameters).checkReturn(implemented.returnTy, body);

	readonly BaseScope baseScope;
	Klass currentClass => baseScope.self;
	readonly MethodOrImpl methodOrImpl;
	readonly bool isStatic;
	readonly Effect selfEffect;
	readonly Arr<Parameter> parameters;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();

	ExprChecker(BaseScope baseScope, MethodOrImpl methodOrImpl, bool isStatic, Effect selfEffect, Arr<Parameter> parameters) {
		this.baseScope = baseScope;
		this.methodOrImpl = methodOrImpl;
		this.isStatic = isStatic;
		this.selfEffect = selfEffect;
		this.parameters = parameters;

		// Assert that parameters don't shadow each other.
		for (uint i = 0; i < parameters.length; i++) {
			var param = parameters[i];
			//Decided to allow parameters to shadow base scope.
			//Example where this is useful: `fun of(Int x) = new x` where `x` is the name of a slot.
			//if (baseScope.hasMember(param.name)) throw TODO();
			for (uint j = 0; j < i; j++)
				if (parameters[j].name == param.name)
					throw TODO();
		}
	}

	Expr checkVoid(Ast.Expr a) => checkSubtype(Ty.Void, a);

	Expr checkInfer(Ast.Expr a) {
		var expected = Expected.Infer();
		return checkExpr(ref expected, a);
	}

	Expr checkReturn(Ty ty, Ast.Expr a) {
		var expected = Expected.Return(ty);
		return checkExpr(ref expected, a);
	}

	Expr checkSubtype(Ty ty, Ast.Expr a) {
		var expected = Expected.SubTypeOf(ty);
		return checkExpr(ref expected, a);
	}

	Expr checkExpr(ref Expected e, Ast.Expr a) {
		switch (a) {
			case Ast.Access ac:
				return checkAccess(ref e, ac);
			case Ast.StaticAccess sa:
				return checkStaticAccess(ref e, sa);
			case Ast.OperatorCall o:
				return checkOperatorCall(ref e, o);
			case Ast.Call c:
				return checkCallAst(ref e, c);
			case Ast.Recur r:
				return checkRecur(ref e, r);
			case Ast.New n:
				return checkNew(ref e, n);
			case Ast.GetProperty g:
				return checkGetProperty(ref e, g);
			case Ast.SetProperty s:
				return checkSetProperty(ref e, s);
			case Ast.Let l:
				return checkLet(ref e, l);
			case Ast.Seq s:
				return checkSeq(ref e, s);
			case Ast.Literal li:
				return checkLiteral(ref e, li);
			case Ast.Self s:
				return checkSelf(ref e, s);
			case Ast.WhenTest w:
				return checkWhenTest(ref e, w);
			case Ast.Assert ass:
				return checkAssert(ref e, ass);
			case Ast.Try t:
				return checkTry(ref e, t);
			default:
				throw unreachable();
		}
	}

	Expr checkAccess(ref Expected expected, Ast.Access a) =>
		handle(ref expected, get(a.loc, a.name));

	static Expr checkStaticAccess(ref Expected expected, Ast.StaticAccess s) {
		//Not in a call, so create a callback
		unused(expected, s);
		throw TODO();
	}

	Expr checkOperatorCall(ref Expected expected, Ast.OperatorCall o) =>
		callMethod(ref expected, o.loc, o.left, o.oper, Arr.of(o.right));

	Expr checkCallAst(ref Expected expected, Ast.Call call) {
		switch (call.target) {
			case Ast.StaticAccess sa:
				var ty = baseScope.accessClsRef(call.loc, sa.className);
				var klass = ty as ClassLike ?? throw TODO();
				return callStaticMethod(ref expected, sa.loc, klass, sa.staticMethodName, call.args);

			case Ast.GetProperty gp:
				return callMethod(ref expected, gp.loc, gp.target, gp.propertyName, call.args);

			case Ast.Access ac:
				return callOwnMethod(ref expected, ac.loc, ac.name, call.args);

			default:
				// Diagnostic -- can't call anything else.
				throw TODO();
		}
	}

	Expr checkRecur(ref Expected expected, Ast.Recur r) {
		if (!expected.inTailCallPosition)
			throw TODO(); //compile error

		var args = this.checkCallArguments(r.loc, parameters, r.args);
		return handle(ref expected, new Recur(r.loc, methodOrImpl, args));
	}

	Expr checkNew(ref Expected expected, Ast.New n) {
		if (!(currentClass.head is KlassHead.Slots slots)) {
			throw TODO(); // Error: Can't `new` an abstract/static class
		}

		if (n.args.length != slots.slots.length)
			throw TODO(); // Not enough / too many fields
		var args = n.args.zip(slots.slots, (arg, slot) => checkSubtype(slot.ty, arg));
		return handle(ref expected, new New(n.loc, slots, args));
	}

	Expr checkGetProperty(ref Expected expected, Ast.GetProperty g) {
		var target = checkInfer(g.target);
		var (targetEffect, targetCls) = target.ty;
		var member = getMember(g.loc, targetCls, g.propertyName);
		if (!(member is Slot slot))
			throw TODO();
		if (slot.mutable && !targetEffect.contains(Effect.Get))
			throw TODO(); // Tried to observe mutable state, but don't have permission.
		// Handling of effect handled by GetSlot.ty -- this is the minimum common effect between 'target' and 'slot.ty'.
		return handle(ref expected, new GetSlot(g.loc, target, slot));
	}

	Expr checkSetProperty(ref Expected expected, Ast.SetProperty s) {
		if (!selfEffect.contains(Effect.Set))
			throw TODO();

		if (!baseScope.tryGetMember(s.propertyName, out var member))
			throw TODO();
		if (!(member is Slot slot))
			throw TODO();

		var value = checkSubtype(slot.ty, s.value);
		return handle(ref expected, new SetSlot(s.loc, slot, value));
	}

	Expr checkLet(ref Expected expected, Ast.Let l) {
		var value = checkInfer(l.value);
		var pattern = startCheckPattern(value.ty, l.assigned, out var nAdded);
		var expr = checkExpr(ref expected, l.then);
		endCheckPattern(nAdded);
		return new Let(l.loc, pattern, value, expr);
	}

	Expr checkSeq(ref Expected expected, Ast.Seq s) {
		var first = checkVoid(s.first);
		var then = checkExpr(ref expected, s.then);
		return new Seq(s.loc, first, then);
	}

	Expr checkLiteral(ref Expected expected, Ast.Literal l) =>
		handle(ref expected, new Literal(l.loc, l.value));

	Expr checkSelf(ref Expected expected, Ast.Self s) =>
		new Self(s.loc, Ty.of(selfEffect, currentClass));

	Expr checkWhenTest(ref Expected expected, Ast.WhenTest w) {
		//Can't use '.map' because of the ref parameter
		var casesBuilder = w.cases.mapBuilder<WhenTest.Case>();
		for (uint i = 0; i < casesBuilder.Length; i++) {
			var kase = w.cases[i];
			var test = checkSubtype(Ty.Bool, kase.test);
			var result = checkExpr(ref expected, kase.result);
			casesBuilder[i] = new WhenTest.Case(kase.loc, test, result);
		}
		var cases = new Arr<WhenTest.Case>(casesBuilder);

		var elseResult = checkExpr(ref expected, w.elseResult);

		return new WhenTest(w.loc, cases, elseResult, expected.inferredType);
	}

	Expr checkAssert(ref Expected expected, Ast.Assert a) =>
		handle(ref expected, new Assert(a.loc, checkSubtype(Ty.Bool, a.asserted)));

	Expr checkTry(ref Expected expected, Ast.Try t) {
		var doo = checkExpr(ref expected, t._do);
		var katch = t._catch.get(out var c) ? Op.Some(checkCatch(ref expected, c)) : Op<Try.Catch>.None;
		var finallee = t._finally.get(out var f) ? Op.Some(checkVoid(f)) : Op<Expr>.None;
		return new Try(t.loc, doo, katch, finallee, expected.inferredType);
	}

	Try.Catch checkCatch(ref Expected expected, Ast.Try.Catch c) {
		var exceptionTy = baseScope.getTy(c.exceptionTy);
		var caught = new Pattern.Single(c.exceptionNameLoc, exceptionTy, c.exceptionName);
		addToScope(caught);
		var then = checkExpr(ref expected, c.then);
		popFromScope();
		return new Try.Catch(c.loc, caught, then);
	}

	Expr callStaticMethod(ref Expected expected, Loc loc, ClassLike klass, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!klass.membersMap.get(methodName, out var member)) TODO();
		if (!(member is MethodWithBodyLike method))
			throw TODO();
		if (!method.isStatic)
			throw TODO();
		// No need to check selfEffect, because this is a static method.
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new StaticMethodCall(loc, method, args));
	}

	Expr callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Expr> argAsts) {
		var target = checkInfer(targetAst);
		var member = getMember(loc, target.ty.cls, methodName);
		if (!(member is Method method))
			throw TODO();
		if (method.isStatic) throw TODO(); //error
		if (!target.ty.effect.contains(method.selfEffect))
			throw TODO(); // Can't call a method on an object with a greater effect than we've declared for it.
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new InstanceMethodCall(loc, target, method, args));
	}

	Expr callOwnMethod(ref Expected expected, Loc loc, Sym methodName, Arr<Ast.Expr> argAsts) {
		var member = getMember(loc, currentClass, methodName);
		if (!(member is Method method))
			throw TODO();
		if (!isStatic && !selfEffect.contains(method.selfEffect))
			throw TODO(); //Can't call my method with a greater effect
		var args = checkCall(loc, method, argAsts);
		var call = method is MethodWithBodyLike mbl && mbl.isStatic ? new StaticMethodCall(loc, mbl, args) : (Expr)new MyInstanceMethodCall(loc, method, args);
		return handle(ref expected, call);
	}

	//Caller is responsible for checking that we can access this member's effect.
	static Member getMember(Loc loc, ClsRef cls, Sym memberName) {
		if (tryGetMember(cls, memberName, out var member))
			return member;

		unused(loc);
		throw TODO(); //error
	}

	static bool tryGetMember(ClsRef cls, Sym memberName, out Member member) {
		var klass = (ClassLike)cls; //TODO: support getting member of builtin class
		if (klass.membersMap.get(memberName, out member)) {
			return true;
		}

		foreach (var super in klass.supers) {
			if (tryGetMember(super.superClass, memberName, out member))
				return true;
		}

		return false;
	}

	// Note: Caller is responsible for checking selfEffect
	Arr<Expr> checkCall(Loc callLoc, Method method, Arr<Ast.Expr> argAsts) =>
		checkCallArguments(callLoc, method.parameters, argAsts);

	// Used by normal calls and by 'recur' (which doesn't need an effect check)
	Arr<Expr> checkCallArguments(Loc loc, Arr<Parameter> parameters, Arr<Ast.Expr> argAsts) {
		if (parameters.length != argAsts.length) {
			unused(loc);
			throw TODO();
		}
		return parameters.zip(argAsts, (parameter, argAst) => checkSubtype(parameter.ty, argAst));
	}

	void addToScope(Pattern.Single local) {
		if (parameters.find(out var param, p => p.name == local.name))
			throw TODO(); //Illegal shadowing.
		if (locals.find(out var shadowedLocal, l => l.name == local.name))
			throw TODO(); //Illegal shadowing.
		locals.Push(local);
	}
	void popFromScope() {
		locals.Pop();
	}

	Expr get(Loc loc, Sym name) {
		if (locals.find(out var local, l => l.name == name))
			return new AccessLocal(loc, local);

		if (parameters.find(out var param, p => p.name == name))
			return new AccessParameter(loc, param);

		if (!baseScope.tryGetMember(name, out var member))
			throw TODO(); //error: cannot find name...

		switch (member) {
			case Slot slot:
				if (isStatic) throw TODO();
				return new GetMySlot(loc, currentClass, slot);

			case MethodWithBody m:
				throw TODO();

			case AbstractMethod a:
				throw TODO();

			default:
				// Not possible for this to be a BuiltinMethod, because it's a method on myself
				throw unreachable();
		}
	}

	Expr handle(ref Expected expected, Expr e) =>
		expected.handle(e, this);

	/** PASS BY REF! */
	struct Expected {
		enum Kind {
			/** Like SubTypeOf, but it's a tail call. */
			Return,
			SubTypeOf,
			Infer
		}
		readonly Kind kind;
		//For Void, this is always null.
		//For SubTypeOf, this is always non-null.
		//For Infer, this is mutable.
		Op<Ty> expectedTy;
		Expected(Kind kind, Op<Ty> expectedTy) {
			this.kind = kind;
			this.expectedTy = expectedTy;
		}

		internal bool inTailCallPosition => kind == Kind.Return;

		internal static Expected Return(Ty ty) => new Expected(Kind.Return, Op.Some(ty));
		internal static Expected SubTypeOf(Ty ty) => new Expected(Kind.SubTypeOf, Op.Some(ty));
		internal static Expected Infer() => new Expected(Kind.Infer, Op<Ty>.None);

		/** Note: This may be called on SubTypeOf. */
		internal Ty inferredType => expectedTy.force;

		internal Expr handle(Expr e, ExprChecker c) {
			switch (kind) {
				case Kind.Return:
				case Kind.SubTypeOf:
					//Ty must be a subtype of this.
					return c.checkType(expectedTy.force, e);
				case Kind.Infer:
					if (expectedTy.get(out var ety)) {
						expectedTy = Op.Some(c.getCombinedType(ety, e.ty));
						return e;
					} else {
						expectedTy = Op.Some(e.ty);
						return e;
					}
				default:
					throw unreachable();
			}
		}
	}

	Ty getCombinedType(Ty a, Ty b) {
		var effect = a.effect.minCommonEffect(b.effect);
		if (!a.cls.fastEquals(b.cls)) {
			unused(this);
			throw TODO();
		}
		return Ty.of(effect, a.cls);
	}

	//mv
	Expr checkType(Ty expectedTy, Expr e) {
		//TODO: subtyping!
		if (isSubtype(expectedTy, e.ty))
			return e;

		//TODO: have a WrongCast node and issue a diagnostic.
		unused(this);
		throw TODO();
	}

	bool isSubtype(Ty expectedTy, Ty actualTy) =>
		actualTy.effect.contains(expectedTy.effect) && // Pure `Foo` can't be assigned to `io Foo`.
			isSubclass(expectedTy.cls, actualTy.cls);

	bool isSubclass(ClsRef expected, ClsRef actual) {
		if (expected.fastEquals(actual))
			return true;
		foreach (var s in actual.supers) {
			if (isSubclass(expected, s.superClass))
				return true;
		}
		return false;
	}

	/**
	 * MUST be used like so:
	 * var i = startCheckPattern(ty, pattern, out var nAdded); // Adds things to scope
	 * ...do things with pattern variables in scope...
	 * endCheckPattern(nAdded); // Removes them from scope
	 */
	Pattern startCheckPattern(Ty ty, Ast.Pattern ast, out uint nAdded) {
		switch (ast) {
			case Ast.Pattern.Ignore i:
				nAdded = 0;
				return new Pattern.Ignore(i.loc);
			case Ast.Pattern.Single p:
				var s = new Pattern.Single(p.loc, ty, p.name);
				nAdded = 1;
				addToScope(s);
				return s;
			case Ast.Pattern.Destruct d:
				throw TODO();
			default:
				throw unreachable();
		}
	}

	void endCheckPattern(uint nAdded) =>
		doTimes(nAdded, popFromScope);
}
