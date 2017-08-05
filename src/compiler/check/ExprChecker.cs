using System.Collections.Generic;

using Diag;
using Diag.CheckExprDiags;
using Model;
using static ClassUtils;
using static TyUtils;
using static Utils;

/** Use a different type than Expr to ensure that I remember handle 'expected'. */
struct Handled {
	internal readonly Expr expr;
	internal Handled(Expr expr) { this.expr = expr; }
}

class ExprChecker : CheckerCommon {
	internal static Expr checkMethod(BaseScope baseScope, Arr.Builder<Diagnostic> diags, MethodOrImpl methodOrImpl, TyReplacer methodReplacer, bool isStatic, Ast.Expr body) {
		var implemented = methodOrImpl.implementedMethod;
		var ckr = new ExprChecker(baseScope, diags, methodOrImpl, methodReplacer, isStatic, implemented.selfEffect, implemented.parameters);
		var returnTy = TyUtils.instantiateType(implemented.returnTy, methodReplacer);
		return ckr.checkReturn(returnTy, body);
	}

	ClassDeclaration currentClass => baseScope.currentClass;
	readonly InstCls currentInstCls;
	readonly MethodOrImpl methodOrImpl;
	readonly bool isStatic;
	readonly Effect selfEffect;
	// When accessing `currentParameters`, must remember to replace types in case this is an implementation of an abstract method and the superclass took type parameters.
	readonly Arr<Parameter> currentParameters;
	readonly TyReplacer currentMethodTyReplacer;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();

	ExprChecker(BaseScope baseScope, Arr.Builder<Diagnostic> diags, MethodOrImpl methodOrImpl, TyReplacer methodReplacer, bool isStatic, Effect selfEffect, Arr<Parameter> parameters) : base(baseScope, diags) {
		this.methodOrImpl = methodOrImpl;
		this.isStatic = isStatic;
		this.selfEffect = selfEffect;
		this.currentParameters = parameters;
		this.currentMethodTyReplacer = methodReplacer;
		currentInstCls = InstCls.of(currentClass, currentClass.typeParameters.map<Ty>(tp => tp));
	}

	Expr checkVoid(Ast.Expr a) => checkSubtype(Ty.Void, a);
	Expr checkBool(Ast.Expr a) => checkSubtype(Ty.Bool, a);

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

	Expr checkExpr(ref Expected e, Ast.Expr a) =>
		checkExprWorker(ref e, a).expr;

	Handled checkExprWorker(ref Expected e, Ast.Expr a) {
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
			case Ast.IfElse i:
				return checkIfElse(ref e, i);
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

	Handled checkAccess(ref Expected expected, Ast.Access access) {
		var (loc, name) = access;

		if (locals.find(out var local, l => l.name == name))
			return handle(ref expected, new AccessLocal(loc, local));

		if (currentParameters.find(out var param, p => p.name == name)) {
			var ty = instantiateType(param.ty, currentMethodTyReplacer);
			return handle(ref expected, new AccessParameter(loc, param, ty));
		}

		if (!baseScope.getOwnMemberOrAddDiagnostic(loc, name, diags, out var member))
			return handleBogus(ref expected, loc);

		var (memberDecl, memberTyReplacer) = member;

		switch (memberDecl) {
			case SlotDeclaration slot:
				return getOwnSlot(ref expected, loc, slot, memberTyReplacer);

			case MethodWithBody _:
			case AbstractMethod _:
				addDiagnostic(loc, DelegatesNotYetSupported.instance);
				return handleBogus(ref expected, loc);

			default:
				// Not possible for this to be a BuiltinMethod, because it's a method on myself
				throw unreachable();
		}
	}

	Handled checkStaticAccess(ref Expected expected, Ast.StaticAccess ast) {
		// Only get here if this is *not* the target of a call.
		var (loc, className, staticMethodName) = ast;
		unused(className, staticMethodName);
		addDiagnostic(ast.loc, DelegatesNotYetSupported.instance);
		return handleBogus(ref expected, ast.loc);
	}

	Handled checkOperatorCall(ref Expected expected, Ast.OperatorCall ast) {
		var (loc, left, oper, right) = ast;
		var tyArgs = Arr.empty<Ast.Ty>(); // No way to provide these to an operator call.
		return callMethod(ref expected, loc, left, oper, tyArgs, Arr.of(right));
	}

	Handled checkCallAst(ref Expected expected, Ast.Call call) {
		var (callLoc, target, tyArgs, args) = call;
		switch (target) {
			case Ast.StaticAccess sa: {
				var (accessLoc, className, staticMethodName) = sa;
				if (!baseScope.accessClassDeclarationOrAddDiagnostic(accessLoc, diags,  className, out var cls))
					return handleBogus(ref expected, accessLoc);
				return callStaticMethod(ref expected, callLoc, cls, staticMethodName, tyArgs, args);
			}

			case Ast.GetProperty getProp:
				var (_, propTarget, propertyName) = getProp;
				return callMethod(ref expected, callLoc, propTarget, propertyName, tyArgs, args);

			case Ast.Access ac:
				return callOwnMethod(ref expected, callLoc, ac.name, tyArgs, args);

			default:
				addDiagnostic(callLoc, DelegatesNotYetSupported.instance);
				return handleBogus(ref expected, callLoc);
		}
	}

	Handled checkRecur(ref Expected expected, Ast.Recur ast) {
		var (loc, argAsts) = ast;

		if (!expected.inTailCallPosition)
			addDiagnostic(loc, NotATailCall.instance);

		// For recursion, need to do substitution in the case that we are implementing an abstract method where the superclass took type arguments.
		if (!checkCallArguments(loc, methodOrImpl.implementedMethod, /*targetReplacer*/TyReplacer.doNothingReplacer, currentMethodTyReplacer, argAsts, out var args))
			return handleBogus(ref expected, loc);

		return handle(ref expected, new Recur(loc, methodOrImpl, args));
	}

	Handled checkNew(ref Expected expected, Ast.New ast) {
		var (loc, tyArgAsts, argAsts) = ast;

		if (!(currentClass.head is ClassHead.Slots slots)) {
			addDiagnostic(loc, new NewInvalid(currentClass));
			return handleBogus(ref expected, loc);
		}

		if (argAsts.length != slots.slots.length) {
			addDiagnostic(loc, new NewArgumentCountMismatch(slots, argAsts.length));
			return handleBogus(ref expected, loc);
		}

		var tyArgs = tyArgAsts.map(getTy);

		if (currentClass.typeParameters.length != tyArgs.length)
			throw TODO();

		var args = argAsts.zip(slots.slots, (arg, slot) => checkSubtype(slot.ty, arg));
		return handle(ref expected, new New(loc, slots, tyArgs, args));
	}

	Handled getOwnSlot(ref Expected expected, Loc loc, SlotDeclaration slot, TyReplacer replacer) {
		if (isStatic) {
			addDiagnostic(loc, new CantAccessSlotFromStaticMethod(slot));
			return handleBogus(ref expected, loc);
		}

		if (slot.mutable && !selfEffect.canGet)
			addDiagnostic(loc, new MissingEffectToGetSlot(slot));

		var slotTy = instantiateTypeAndNarrowEffects(selfEffect, slot.ty, replacer, loc, diags);
		return handle(ref expected, new GetMySlot(loc, slot, slotTy));
	}

	Handled checkGetProperty(ref Expected expected, Ast.GetProperty ast) {
		var (loc, targetAst, propertyName) = ast;

		var target = checkInfer(targetAst);
		switch (target.ty) {
			case BogusTy _:
				return handleBogus(ref expected, loc);

			case PlainTy plainTy: {
				var (targetEffect, targetCls) = plainTy;
				if (!getMemberOfInstClsOrAddDiagnostic(loc, targetCls, propertyName, out var member))
					return handleBogus(ref expected, loc);

				if (!(member.memberDecl is SlotDeclaration slot)) {
					addDiagnostic(loc, DelegatesNotYetSupported.instance);
					return handleBogus(ref expected, loc);
				}

				if (slot.mutable && !targetEffect.canGet)
					addDiagnostic(loc, new MissingEffectToGetSlot(slot));

				var slotTy = instantiateTypeAndNarrowEffects(targetEffect, slot.ty, member.replacer, loc, diags);

				// Handling of effect handled by GetSlot.ty -- this is the minimum common effect between 'target' and 'slot.ty'.
				return handle(ref expected, new GetSlot(loc, target, slot, slotTy));
			}

			default:
				throw unreachable();
		}
	}

	Handled checkSetProperty(ref Expected expected, Ast.SetProperty ast) {
		var (loc, propertyName, valueAst) = ast;

		if (!baseScope.getOwnMemberOrAddDiagnostic(loc, propertyName, diags, out var member))
			return handleBogus(ref expected, loc);

		var (memberDecl, replacer) = member;

		if (!(memberDecl is SlotDeclaration slot)) {
			addDiagnostic(loc, new CantSetNonSlot(memberDecl));
			return handleBogus(ref expected, loc);
		}

		if (!slot.mutable)
			addDiagnostic(loc, new SlotNotMutable(slot));

		if (!selfEffect.canSet)
			addDiagnostic(loc, new MissingEffectToSetSlot(selfEffect, slot));

		var value = checkSubtype(TyUtils.instantiateType(slot.ty, replacer), valueAst);
		return handle(ref expected, new SetSlot(loc, slot, value));
	}

	Handled checkLet(ref Expected expected, Ast.Let ast) {
		var (loc, assigned, valueAst, thenAst) = ast;
		var value = checkInfer(valueAst);
		var (pattern, nAdded) = startCheckPattern(value.ty, assigned);
		var then = checkExpr(ref expected, thenAst);
		endCheckPattern(nAdded);
		// 'expected' was handled in 'then'
		return new Handled(new Let(ast.loc, pattern, value, then));
	}

	Handled checkSeq(ref Expected expected, Ast.Seq ast) {
		var (loc, firstAst, thenAst) = ast;
		var first = checkVoid(firstAst);
		var then = checkExpr(ref expected, thenAst);
		return handle(ref expected, new Seq(ast.loc, first, then));
	}

	Handled checkLiteral(ref Expected expected, Ast.Literal ast) {
		var (loc, value) = ast;
		return handle(ref expected, new Literal(loc, value));
	}

	Handled checkSelf(ref Expected expected, Ast.Self s) {
		/*
		Create an InstCls for the current class that just maps type parameters to theirselves.
		For example:
			class Foo[T]
				Foo[T] get-self()
					self || This is of type Foo[T] where T is the same as the type parameter on Foo.

			fun use-it(Foo[Int] foo)
				foo.get-self() || The return type has the same T, so it will be instantiated to return Foo[Int].
		*/
		var self = new Self(s.loc, Ty.of(selfEffect, currentInstCls));
		return handle(ref expected, self);
	}

	Handled checkIfElse(ref Expected expected, Ast.IfElse ast) {
		var (loc, conditionAst, thenAst, elseAst) = ast;
		var condition = checkBool(conditionAst);
		var then = checkExpr(ref expected, thenAst);
		var @else = checkExpr(ref expected, elseAst);
		// `expected` was handled in `then` and `else`.
		return new Handled(new IfElse(loc, condition, then, @else, expected.inferredType));
	}

	Handled checkWhenTest(ref Expected expected, Ast.WhenTest ast) {
		var (loc, caseAsts, elseResultAst) = ast;

		//Can't use '.map' because of the `ref expected` parameter
		var casesBuilder = caseAsts.mapBuilder<WhenTest.Case>();
		for (uint i = 0; i < casesBuilder.Length; i++) {
			var kase = caseAsts[i];
			var test = checkBool(kase.test);
			var result = checkExpr(ref expected, kase.result);
			casesBuilder[i] = new WhenTest.Case(kase.loc, test, result);
		}
		var cases = new Arr<WhenTest.Case>(casesBuilder);

		var elseResult = checkExpr(ref expected, elseResultAst);

		// `expected` was handled in each case result.
		return new Handled(new WhenTest(loc, cases, elseResult, expected.inferredType));
	}

	Handled checkAssert(ref Expected expected, Ast.Assert ast) {
		var (loc, asserted) = ast;
		return handle(ref expected, new Assert(loc, checkBool(asserted)));
	}

	Handled checkTry(ref Expected expected, Ast.Try ast) {
		var (loc, doAst, catchAst, finallyAst) = ast;
		var doo = checkExpr(ref expected, doAst);
		var katch = catchAst.get(out var c) ? Op.Some(checkCatch(ref expected, c)) : Op<Try.Catch>.None;
		var finallee = finallyAst.get(out var f) ? Op.Some(checkVoid(f)) : Op<Expr>.None;
		// 'expected' handled when checking 'do' and 'catch'
		return new Handled(new Try(loc, doo, katch, finallee, expected.inferredType));
	}

	Try.Catch checkCatch(ref Expected expected, Ast.Try.Catch ast) {
		//out Loc loc, out Ty ty, out Loc exceptionNameLoc, out Sym exceptionName, out Expr then) {
		var (loc, exceptionTyAst, exceptionNameLoc, exceptionName, thenAst) = ast;

		var exceptionTy = getTy(exceptionTyAst);
		var caught = new Pattern.Single(exceptionNameLoc, exceptionTy, exceptionName);
		addToScope(caught);
		var then = checkExpr(ref expected, thenAst);
		popFromScope();
		return new Try.Catch(loc, caught, then);
	}

	Handled callStaticMethod(ref Expected expected, Loc loc, ClassDeclarationLike cls, Sym methodName, Arr<Ast.Ty> tyArgAsts, Arr<Ast.Expr> argAsts) {
		if (!cls.getMember(methodName, out var member)) { // No need to look in superclasses because this is a static method.
			addDiagnostic(loc, new MemberNotFound(cls, methodName));
			return handleBogus(ref expected, loc);
		}
		if (!(member is MethodWithBodyLike methodDecl) || !methodDecl.isStatic) {
			addDiagnostic(loc, DelegatesNotYetSupported.instance);
			return handleBogus(ref expected, loc);
		}

		if (!instantiateMethodOrAddDiagnostic(methodDecl, tyArgAsts, out var methodInst))
			return handleBogus(ref expected, loc);

		// No need to check selfEffect, because this is a static method.
		// Static methods can't look at their class' type arguments.
		if (!checkCall(loc, methodInst, TyReplacer.doNothingReplacer, argAsts, out var args))
			return handleBogus(ref expected, loc);
		return handle(ref expected, new StaticMethodCall(loc, methodInst, args, instantiateReturnType(methodInst)));
	}

	bool instantiateMethodOrAddDiagnostic(MethodDeclaration methodDecl, Arr<Ast.Ty> tyArgAsts, out MethodInst methodInst) {
		var tyArgs = tyArgAsts.map(getTy);
		if (tyArgs.length != methodDecl.typeParameters.length)
			throw TODO(); // Diagnostic

		methodInst = new MethodInst(methodDecl, tyArgs);
		return true;
	}

	//mv
	static Ty instantiateReturnType(MethodInst method) =>
		instantiateType(method.decl.returnTy, method.replacer);

	Handled callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Ty> tyArgAsts, Arr<Ast.Expr> argAsts) {
		var target = checkInfer(targetAst);
		switch (target.ty) {
			case BogusTy _:
				// Already issued an error, don't need another.
				return handleBogus(ref expected, loc);

			case PlainTy plainTy: {
				var (targetEffect, targetCls) = plainTy;
				if (!getMemberOfInstClsOrAddDiagnostic(loc, targetCls, methodName, out var member))
					return handleBogus(ref expected, loc);

				var (memberDecl, memberReplacer) = member;

				if (!(memberDecl is MethodDeclaration methodDecl)) {
					addDiagnostic(loc, DelegatesNotYetSupported.instance);
					return handleBogus(ref expected, loc);
				}

				if (methodDecl.isStatic) {
					addDiagnostic(loc, new CantAccessStaticMethodThroughInstance(methodDecl));
					return handleBogus(ref expected, loc);
				}

				if (!targetEffect.contains(methodDecl.selfEffect))
					addDiagnostic(loc, new IllegalEffect(targetEffect, methodDecl.selfEffect));

				if (!instantiateMethodOrAddDiagnostic(methodDecl, tyArgAsts, out var methodInst))
					return handleBogus(ref expected, loc);

				if (!checkCall(loc, methodInst, memberReplacer, argAsts, out var args))
					return handleBogus(ref expected, loc);

				return handle(ref expected, new InstanceMethodCall(loc, target, methodInst, args, instantiateReturnType(methodInst)));
			}

			default:
				throw unreachable();
		}
	}

	Handled callOwnMethod(ref Expected expected, Loc loc, Sym methodName, Arr<Ast.Ty> tyArgAsts, Arr<Ast.Expr> argAsts) {
		/*
		Note: InstCls is still relevent here; even if 'self' is not an inst, in a superclass we will fill in type parameters.
		*/
		if (!getMemberOfInstClsOrAddDiagnostic(loc, currentInstCls, methodName, out var member))
			return handleBogus(ref expected, loc);

		var (memberDecl, memberReplacer) = member;

		if (!(memberDecl is MethodDeclaration methodDecl)) {
			addDiagnostic(loc, DelegatesNotYetSupported.instance);
			return handleBogus(ref expected, loc);
		}

		if (!instantiateMethodOrAddDiagnostic(methodDecl, tyArgAsts, out var methodInst))
			return handleBogus(ref expected, loc);

		if (!checkCall(loc, methodInst, memberReplacer, argAsts, out var args))
			return handleBogus(ref expected, loc);

		var ty = instantiateReturnType(methodInst);

		Expr call;
		if (methodInst.isStatic) {
			// Calling a static method. OK whether we're in an instance or static context.
			call = new StaticMethodCall(loc, methodInst, args, ty);
		} else {
			if (isStatic) {
				addDiagnostic(loc, new CantCallInstanceMethodFromStaticMethod(methodDecl));
				return handleBogus(ref expected, loc);
			}

			if (!selfEffect.contains(methodDecl.selfEffect))
				addDiagnostic(loc, new IllegalEffect(selfEffect, methodDecl.selfEffect));

			call = new MyInstanceMethodCall(loc, methodInst, args, ty);
		}

		return handle(ref expected, call);
	}


	/**
	NOTE: Caller is responsible for checking that we can access this member's effect!
	If this returns "false", we've already handled the error reporting, so just call handleBogus.
	*/
	bool getMemberOfInstClsOrAddDiagnostic(Loc loc, InstCls cls, Sym memberName, out InstMember member) {
		if (tryGetMemberOfInstCls(cls, memberName, out member))
			return true;

		addDiagnostic(loc, new MemberNotFound(cls.classDeclaration, memberName));
		return false;
	}

	// Note: Caller is responsible for checking selfEffect
	bool checkCall(Loc callLoc, MethodInst method, TyReplacer targetReplacer, Arr<Ast.Expr> argAsts, out Arr<Expr> args) =>
		checkCallArguments(callLoc, method.decl, targetReplacer, method.replacer, argAsts, out args);

	/**
	`targetReplacer` is the replacer that comes from the instance of an instance method call. E.g.:
		class Box[T]
			T get()

		fun foo(Box[Nat] b)
			b.get() || Must replace T with Nat

	Returns None on error.
	Used by normal calls and by 'recur' (which doesn't need an effect check)

	'methodReplacer' is the one for ordinary generic methods, e.g. `fun f[T]`.
	*/
	bool checkCallArguments(Loc loc, MethodDeclaration method, TyReplacer targetReplacer, TyReplacer methodReplacer, Arr<Ast.Expr> argAsts, out Arr<Expr> args) {
		if (method.parameters.length != argAsts.length) {
			addDiagnostic(loc, new ArgumentCountMismatch(method, argAsts.length));
			args = default(Arr<Expr>); // Caller shouldn't look at this.
			return false;
		}

		var fullReplacer = targetReplacer.combine(methodReplacer);
		args = method.parameters.zip(argAsts, (parameter, argAst) =>
			checkSubtype(instantiateType(parameter.ty, fullReplacer), argAst));
		return true;
	}

	void addToScope(Pattern.Single local) {
		// It's important that we push even in the presence of errors, because we will always pop.

		if (currentParameters.find(out var param, p => p.name == local.name))
			addDiagnostic(local.loc, new CantReassignParameter(param));

		if (locals.find(out var oldLocal, l => l.name == local.name))
			addDiagnostic(local.loc, new CantReassignLocal(oldLocal));

		locals.Push(local);
	}
	void popFromScope() {
		locals.Pop();
	}

	Handled handle(ref Expected expected, Expr e) =>
		expected.handle(e, this);

	static Handled handleBogus(ref Expected expected, Loc loc) =>
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

		internal Handled handle(Expr e, ExprChecker c) {
			switch (kind) {
				case Kind.Return:
				case Kind.SubTypeOf:
					//Ty must be a subtype of this.
					return c.checkType(expectedTy.force, e);
				case Kind.Infer:
					if (expectedTy.get(out var ety)) {
						expectedTy = Op.Some(c.getCompatibleType(e.loc, ety, e.ty));
						return new Handled(e);
					} else {
						expectedTy = Op.Some(e.ty);
						return new Handled(e);
					}
				default:
					throw unreachable();
			}
		}

		internal Handled handleBogus(Loc loc) {
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
			return new Handled(new Bogus(loc, ty));
		}
	}

	// Both `a` and `b` should be subtypes of the result.
	Ty getCompatibleType(Loc loc, Ty a, Ty b) {
		if (getCommonCompatibleType(a, b).get(out var c))
			return c;

		addDiagnostic(loc, new CantCombineTypes(a, b));
		return Ty.bogus;
	}

	Handled checkType(Ty expectedTy, Expr e) {
		if (isAssignable(expectedTy, e.ty))
			return new Handled(e);

		addDiagnostic(e.loc, new NotAssignable(expectedTy, e.ty));
		return new Handled(new BogusCast(expectedTy, e));
	}

	/**
	 * MUST be used like so:
	 * var i = startCheckPattern(ty, pattern, out var nAdded); // Adds things to scope
	 * ...do things with pattern variables in scope...
	 * endCheckPattern(nAdded); // Removes them from scope
	 */
	(Pattern, uint nAdded) startCheckPattern(Ty ty, Ast.Pattern ast) {
		var loc = ast.loc;
		switch (ast) {
			case Ast.Pattern.Ignore i:
				return (new Pattern.Ignore(loc), 0);
			case Ast.Pattern.Single p:
				var s = new Pattern.Single(loc, ty, p.name);
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

//mv
struct InstMember {
	internal readonly MemberDeclaration memberDecl;
	internal readonly TyReplacer replacer;
	internal InstMember(MemberDeclaration memberDecl, TyReplacer replacer) {
		this.memberDecl = memberDecl;
		this.replacer = replacer;
	}
	internal void Deconstruct(out MemberDeclaration memberDecl, out TyReplacer replacer) {
		memberDecl = this.memberDecl;
		replacer = this.replacer;
	}
}

//mv
struct TyReplacer {
	readonly Arr<TypeParameter> typeParameters;
	readonly Arr<Ty> typeArguments;
	TyReplacer(Arr<TypeParameter> typeParameters, Arr<Ty> typeArguments) {
		this.typeParameters = typeParameters;
		this.typeArguments = typeArguments;
	}
	internal static TyReplacer ofInstCls(InstCls i) {
		var (classDeclaration, typeArguments) = i;
		return new TyReplacer(classDeclaration.typeParameters, typeArguments);
	}
	internal static TyReplacer ofMethod(MethodDeclaration m, Arr<Ty> tyArgs) =>
		new TyReplacer(m.typeParameters, tyArgs);

	internal static readonly TyReplacer doNothingReplacer = new TyReplacer(Arr.empty<TypeParameter>(), Arr.empty<Ty>());

	internal Ty replaceOrSame(TypeParameter ty) =>
		replace(ty, out var newTy) ? newTy : ty;

	internal bool replace(TypeParameter ty, out Ty newTy) {
		for (uint i = 0; i < typeParameters.length; i++)
			if (ty.fastEquals(typeParameters[i])) {
				newTy = typeArguments[i];
				return true;
			}
		newTy = default(Ty);
		return false;
	}

	internal TyReplacer combine(TyReplacer other) {
		assert(!typeParameters.some(ta => other.typeParameters.some(ta.fastEquals)));
		return new TyReplacer(typeParameters.concat(other.typeParameters), typeArguments.concat(other.typeArguments));
	}
}
