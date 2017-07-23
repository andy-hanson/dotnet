using System.Collections.Generic;

using Diag;
using Diag.CheckExprDiags;
using Model;
using static TyUtils;
using static Utils;

class ExprChecker : DiagnosticBuilder {
	internal static Expr checkMethod(BaseScope baseScope, Arr.Builder<Diagnostic> diags, MethodOrImpl methodOrImpl, bool isStatic, Method implemented, Ast.Expr body) =>
		new ExprChecker(baseScope, diags, methodOrImpl, isStatic, implemented.selfEffect, implemented.parameters).checkReturn(implemented.returnTy, body);

	readonly BaseScope baseScope;
	Klass currentClass => baseScope.self;
	readonly MethodOrImpl methodOrImpl;
	readonly bool isStatic;
	readonly Effect selfEffect;
	readonly Arr<Parameter> parameters;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();

	ExprChecker(BaseScope baseScope, Arr.Builder<Diagnostic> diags, MethodOrImpl methodOrImpl, bool isStatic, Effect selfEffect, Arr<Parameter> parameters) : base(diags) {
		this.baseScope = baseScope;
		this.methodOrImpl = methodOrImpl;
		this.isStatic = isStatic;
		this.selfEffect = selfEffect;
		this.parameters = parameters;
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

	Expr checkAccess(ref Expected expected, Ast.Access a) {
		var loc = a.loc; var name = a.name; //TODO:destructuring

		if (locals.find(out var local, l => l.name == name))
			return handle(ref expected, new AccessLocal(loc, local));

		if (parameters.find(out var param, p => p.name == name))
			return handle(ref expected, new AccessParameter(loc, param));

		if (!baseScope.tryGetMember(name, out var member)) {
			addDiagnostic(loc, new MemberNotFound(currentClass, name));
			return handleBogus(ref expected, loc);
		}

		switch (member) {
			case Slot slot:
				if (isStatic) {
					addDiagnostic(loc, new CantAccessSlotFromStaticMethod(slot));
					return handleBogus(ref expected, loc);
				}

				if (slot.mutable && selfEffect.canGet())
					addDiagnostic(loc, new MissingEffectToGetSlot(slot));

				return handle(ref expected, new GetMySlot(loc, currentClass, slot));

			case MethodWithBody _:
			case AbstractMethod _:
				addDiagnostic(loc, DelegatesNotYetSupported.instance);
				return handleBogus(ref expected, loc);

			default:
				// Not possible for this to be a BuiltinMethod, because it's a method on myself
				throw unreachable();
		}
	}

	Expr checkStaticAccess(ref Expected expected, Ast.StaticAccess s) {
		// Only get here if this is *not* the target of a call.
		addDiagnostic(s.loc, DelegatesNotYetSupported.instance);
		return handleBogus(ref expected, s.loc);
	}

	Expr checkOperatorCall(ref Expected expected, Ast.OperatorCall o) =>
		callMethod(ref expected, o.loc, o.left, o.oper, Arr.of(o.right));

	Expr checkCallAst(ref Expected expected, Ast.Call call) {
		var loc = call.loc;
		switch (call.target) {
			case Ast.StaticAccess sa:
				var cls = baseScope.accessClsRef(call.loc, sa.className);
				return callStaticMethod(ref expected, loc, cls, sa.staticMethodName, call.args);

			case Ast.GetProperty gp:
				return callMethod(ref expected, loc, gp.target, gp.propertyName, call.args);

			case Ast.Access ac:
				return callOwnMethod(ref expected, loc, ac.name, call.args);

			default:
				addDiagnostic(loc, DelegatesNotYetSupported.instance);
				return handleBogus(ref expected, loc);
		}
	}

	Expr checkRecur(ref Expected expected, Ast.Recur r) {
		var loc = r.loc;

		if (!expected.inTailCallPosition)
			addDiagnostic(loc, NotATailCall.instance);

		if (!this.checkCallArguments(loc, methodOrImpl.implementedMethod, parameters, r.args, out var args))
			return handleBogus(ref expected, loc);

		return handle(ref expected, new Recur(loc, methodOrImpl, args));
	}

	Expr checkNew(ref Expected expected, Ast.New n) {
		var loc = n.loc; var argAsts = n.args; //TODO:destructure

		if (!(currentClass.head is KlassHead.Slots slots)) {
			addDiagnostic(loc, new NewInvalid(currentClass));
			return handleBogus(ref expected, loc);
		}

		if (argAsts.length != slots.slots.length) {
			addDiagnostic(loc, new NewArgumentCountMismatch(slots, argAsts.length));
			return handleBogus(ref expected, loc);
		}

		var args = argAsts.zip(slots.slots, (arg, slot) => checkSubtype(slot.ty, arg));
		return handle(ref expected, new New(loc, slots, args));
	}

	Expr checkGetProperty(ref Expected expected, Ast.GetProperty g) {
		var loc = g.loc;
		var target = checkInfer(g.target);
		switch (target.ty) {
			case Ty.Bogus _:
				return handleBogus(ref expected, loc);

			case Ty.PlainTy plainTy: {
				var (targetEffect, targetCls) = plainTy;
				if (!getMember(loc, targetCls, g.propertyName, out var member))
					return handleBogus(ref expected, loc);

				if (!(member is Slot slot)) {
					addDiagnostic(loc, DelegatesNotYetSupported.instance);
					return handleBogus(ref expected, loc);
				}

				if (slot.mutable && !targetEffect.canGet())
					addDiagnostic(loc, new MissingEffectToGetSlot(slot));

				// Handling of effect handled by GetSlot.ty -- this is the minimum common effect between 'target' and 'slot.ty'.
				return handle(ref expected, new GetSlot(loc, target, slot));
			}

			default:
				throw unreachable();
		}
	}

	Expr checkSetProperty(ref Expected expected, Ast.SetProperty s) {
		var loc = s.loc;

		if (!baseScope.tryGetMember(s.propertyName, out var member)) {
			addDiagnostic(loc, new MemberNotFound(currentClass, s.propertyName));
			return handleBogus(ref expected, loc);
		}

		if (!(member is Slot slot)) {
			addDiagnostic(loc, new CantSetNonSlot(member));
			return handleBogus(ref expected, loc);
		}

		if (!slot.mutable)
			addDiagnostic(loc, new SlotNotMutable(slot));

		if (!selfEffect.contains(Effect.Set))
			addDiagnostic(loc, new MissingEffectToSetSlot(slot));

		var value = checkSubtype(slot.ty, s.value);
		return handle(ref expected, new SetSlot(loc, slot, value));
	}

	Expr checkLet(ref Expected expected, Ast.Let l) {
		var value = checkInfer(l.value);
		var (pattern, nAdded) = startCheckPattern(value.ty, l.assigned);
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

	Expr callStaticMethod(ref Expected expected, Loc loc, ClsRef cls, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!cls.getMember(methodName, out var member)) { // No need to look in superclasses because this is a static method.
			addDiagnostic(loc, new MemberNotFound(cls, methodName));
			return handleBogus(ref expected, loc);
		}
		if (!(member is MethodWithBodyLike method) || !method.isStatic) {
			addDiagnostic(loc, DelegatesNotYetSupported.instance);
			return handleBogus(ref expected, loc);
		}

		// No need to check selfEffect, because this is a static method.
		if (!checkCall(loc, method, argAsts, out var args))
			return handleBogus(ref expected, loc);
		return handle(ref expected, new StaticMethodCall(loc, method, args));
	}

	Expr callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Expr> argAsts) {
		var target = checkInfer(targetAst);
		switch (target.ty) {
			case Ty.Bogus _:
				// Already issued an error, don't need another.
				return handleBogus(ref expected, loc);

			case Ty.PlainTy plainTy: {
				var (targetEffect, targetCls) = plainTy;
				if (!getMember(loc, targetCls, methodName, out var member))
					return handleBogus(ref expected, loc);

				if (!(member is Method method)) {
					addDiagnostic(loc, DelegatesNotYetSupported.instance);
					return handleBogus(ref expected, loc);
				}

				if (method.isStatic) {
					addDiagnostic(loc, new CantAccessStaticMethodThroughInstance(method));
					return handleBogus(ref expected, loc);
				}

				if (!targetEffect.contains(method.selfEffect))
					addDiagnostic(loc, new IllegalEffect(targetEffect, method.selfEffect));

				if (!checkCall(loc, method, argAsts, out var args))
					return handleBogus(ref expected, loc);
				return handle(ref expected, new InstanceMethodCall(loc, target, method, args));
			}

			default:
				throw unreachable();
		}
	}

	Expr callOwnMethod(ref Expected expected, Loc loc, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!getMember(loc, currentClass, methodName, out Member member))
			return handleBogus(ref expected, loc);

		if (!(member is Method method)) {
			addDiagnostic(loc, DelegatesNotYetSupported.instance);
			return handleBogus(ref expected, loc);
		}

		if (!checkCall(loc, method, argAsts, out var args))
			return handleBogus(ref expected, loc);

		Expr call;
		if (method is MethodWithBodyLike mbl && mbl.isStatic) {
			// Calling a static method. OK whether we're in an instance or static context.
			call = new StaticMethodCall(loc, mbl, args);
		} else {
			if (isStatic) {
				addDiagnostic(loc, new CantCallInstanceMethodFromStaticMethod(method));
				return handleBogus(ref expected, loc);
			}

			if (!selfEffect.contains(method.selfEffect))
				addDiagnostic(loc, new IllegalEffect(selfEffect, method.selfEffect));

			call = new MyInstanceMethodCall(loc, method, args);
		}

		return handle(ref expected, call);
	}

	/**
	NOTE: Caller is responsible for checking that we can access this member's effect!
	If this returns "false", we've already handled the error reporting, so just call handleBogus.
	*/
	bool getMember(Loc loc, ClsRef cls, Sym memberName, out Member member) {
		if (tryGetMember(cls, memberName, out member))
			return true;

		addDiagnostic(loc, new MemberNotFound(cls, memberName));
		return false;
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
	bool checkCall(Loc callLoc, Method method, Arr<Ast.Expr> argAsts, out Arr<Expr> args) =>
		checkCallArguments(callLoc, method, method.parameters, argAsts, out args);

	/**
	Returns None on error.
	Used by normal calls and by 'recur' (which doesn't need an effect check)
	*/
	bool checkCallArguments(Loc loc, Method method, Arr<Parameter> parameters, Arr<Ast.Expr> argAsts, out Arr<Expr> args) {
		if (parameters.length != argAsts.length) {
			addDiagnostic(loc, new ArgumentCountMismatch(method, argAsts.length));
			args = default(Arr<Expr>); // Caller shouldn't look at this.
			return false;
		}
		args = parameters.zip(argAsts, (parameter, argAst) => checkSubtype(parameter.ty, argAst));
		return true;
	}

	void addToScope(Pattern.Single local) {
		// It's important that we push even in the presence of errors, because we will always pop.

		if (parameters.find(out var param, p => p.name == local.name))
			addDiagnostic(local.loc, new CantReassignParameter(param));

		if (locals.find(out var oldLocal, l => l.name == local.name))
			addDiagnostic(local.loc, new CantReassignLocal(oldLocal));

		locals.Push(local);
	}
	void popFromScope() {
		locals.Pop();
	}

	Expr handle(ref Expected expected, Expr e) =>
		expected.handle(e, this);

	static Expr handleBogus(ref Expected expected, Loc loc) =>
		expected.handleBogus(loc);

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
						expectedTy = Op.Some(c.combineTypes(e.loc, ety, e.ty));
						return e;
					} else {
						expectedTy = Op.Some(e.ty);
						return e;
					}
				default:
					throw unreachable();
			}
		}

		internal Expr handleBogus(Loc loc) {
			Ty ty;
			switch (kind) {
				case Kind.Return:
				case Kind.SubTypeOf:
					ty = expectedTy.force;
					break;
				case Kind.Infer:
					if (!expectedTy.get(out ty)) {
						ty = Ty.bogus;
						expectedTy = Op.Some(ty);
					}
					break;
				default:
					throw unreachable();
			}
			return new Bogus(loc, ty);
		}
	}

	Ty combineTypes(Loc loc, Ty a, Ty b) {
		var combined = getCombinedType(a, b);
		if (combined.get(out var c))
			return c;

		addDiagnostic(loc, new CantCombineTypes(a, b));
		return Ty.bogus;
	}

	Expr checkType(Ty expectedTy, Expr e) {
		if (isAssignable(expectedTy, e.ty))
			return e;

		addDiagnostic(e.loc, new NotAssignable(expectedTy, e.ty));
		return new BogusCast(expectedTy, e);
	}

	/**
	 * MUST be used like so:
	 * var i = startCheckPattern(ty, pattern, out var nAdded); // Adds things to scope
	 * ...do things with pattern variables in scope...
	 * endCheckPattern(nAdded); // Removes them from scope
	 */
	(Pattern, uint nAdded) startCheckPattern(Ty ty, Ast.Pattern ast) {
		switch (ast) {
			case Ast.Pattern.Ignore i:
				return (new Pattern.Ignore(i.loc), 0);
			case Ast.Pattern.Single p:
				var s = new Pattern.Single(p.loc, ty, p.name);
				addToScope(s);
				return (s, 1);
			case Ast.Pattern.Destruct d:
				throw TODO();
			default:
				throw unreachable();
		}
	}

	void endCheckPattern(uint nAdded) =>
		doTimes(nAdded, popFromScope);
}
