using System.Collections.Generic;

using Model;
using static Utils;

class ExprChecker {
	internal static Expr checkMethod(BaseScope baseScope, MethodOrImpl methodOrImpl, bool isStatic, Ty returnTy, Arr<Parameter> parameters, Effect effect, Ast.Expr body) =>
		new ExprChecker(baseScope, methodOrImpl, isStatic, parameters, effect).checkReturn(returnTy, body);

	readonly BaseScope baseScope;
	Klass currentClass => baseScope.self;
	readonly MethodOrImpl methodOrImpl;
	readonly bool isStatic;
	readonly Arr<Parameter> parameters;
	/** Declared effect for this method. Cannot call methods that have effects we haven't declared. */
	readonly Effect effect;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();

	ExprChecker(BaseScope baseScope, MethodOrImpl methodOrImpl, bool isStatic, Arr<Parameter> parameters, Effect effect) {
		this.baseScope = baseScope;
		this.methodOrImpl = methodOrImpl;
		this.isStatic = isStatic;
		this.parameters = parameters;
		this.effect = effect;

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

	Expr checkVoid(Ast.Expr a) => checkSubtype(BuiltinClass.Void, a);

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
			case Ast.Expr.Access ac:
				return checkAccess(ref e, ac);
			case Ast.Expr.StaticAccess sa:
				return checkStaticAccess(ref e, sa);
			case Ast.Expr.OperatorCall o:
				return checkOperatorCall(ref e, o);
			case Ast.Expr.Call c:
				return checkCallAst(ref e, c);
			case Ast.Expr.Recur r:
				return checkRecur(ref e, r);
			case Ast.Expr.New n:
				return checkNew(ref e, n);
			case Ast.Expr.GetProperty g:
				return checkGetProperty(ref e, g);
			case Ast.Expr.Let l:
				return checkLet(ref e, l);
			case Ast.Expr.Seq s:
				return checkSeq(ref e, s);
			case Ast.Expr.Literal li:
				return checkLiteral(ref e, li);
			case Ast.Expr.Self s:
				return checkSelf(ref e, s);
			case Ast.Expr.WhenTest w:
				return checkWhenTest(ref e, w);
			case Ast.Expr.Assert ass:
				return checkAssert(ref e, ass);
			case Ast.Expr.Try t:
				return checkTry(ref e, t);
			default:
				throw unreachable();
		}
	}

	Expr checkAccess(ref Expected expected, Ast.Expr.Access a) =>
		handle(ref expected, get(a.loc, a.name));

	static Expr checkStaticAccess(ref Expected expected, Ast.Expr.StaticAccess s) {
		//Not in a call, so create a callback
		unused(expected, s);
		throw TODO();
	}

	Expr checkOperatorCall(ref Expected expected, Ast.Expr.OperatorCall o) =>
		callMethod(ref expected, o.loc, o.left, o.oper, Arr.of(o.right));

	Expr checkCallAst(ref Expected expected, Ast.Expr.Call call) {
		switch (call.target) {
			case Ast.Expr.StaticAccess sa:
				var ty = baseScope.accessTy(call.loc, sa.className);
				var klass = ty as ClassLike ?? throw TODO();
				return callStaticMethod(ref expected, sa.loc, klass, sa.staticMethodName, call.args);

			case Ast.Expr.GetProperty gp:
				return callMethod(ref expected, gp.loc, gp.target, gp.propertyName, call.args);

			case Ast.Expr.Access ac:
				return callOwnMethod(ref expected, ac.loc, ac.name, call.args);

			default:
				// Diagnostic -- can't call anything else.
				throw TODO();
		}
	}

	Expr checkRecur(ref Expected expected, Ast.Expr.Recur r) {
		if (!expected.inTailCallPosition)
			throw TODO(); //compile error

		//should match this.parameters
		var loc = r.loc;
		var args = this.checkCallArguments(loc, parameters, r.args);
		return handle(ref expected, new Expr.Recur(loc, methodOrImpl, args));
	}

	Expr checkNew(ref Expected expected, Ast.Expr.New n) {
		if (!(currentClass.head is Klass.Head.Slots slots)) {
			throw TODO(); // Error: Can't `new` an abstract/static class
		}

		if (n.args.length != slots.slots.length)
			throw TODO(); // Not enough / too many fields
		var args = n.args.zip(slots.slots, (arg, slot) => checkSubtype(slot.ty, arg));
		return handle(ref expected, new Expr.New(n.loc, slots, args));
	}

	Expr checkGetProperty(ref Expected expected, Ast.Expr.GetProperty g) {
		var target = checkInfer(g.target);
		var member = getMember(g.loc, target.ty, g.propertyName);
		var slot = (Slot)member; //TODO: error handling
		return handle(ref expected, new Expr.GetSlot(g.loc, target, slot));
	}

	Expr checkLet(ref Expected expected, Ast.Expr.Let l) {
		var value = checkInfer(l.value);
		var pattern = startCheckPattern(value.ty, l.assigned, out var nAdded);
		var expr = checkExpr(ref expected, l.then);
		endCheckPattern(nAdded);
		return new Expr.Let(l.loc, pattern, value, expr);
	}

	Expr checkSeq(ref Expected expected, Ast.Expr.Seq s) {
		var first = checkVoid(s.first);
		var then = checkExpr(ref expected, s.then);
		return new Expr.Seq(s.loc, first, then);
	}

	Expr checkLiteral(ref Expected expected, Ast.Expr.Literal l) =>
		handle(ref expected, new Expr.Literal(l.loc, l.value));

	Expr checkSelf(ref Expected expected, Ast.Expr.Self s) => new Expr.Self(s.loc, currentClass);

	Expr checkWhenTest(ref Expected expected, Ast.Expr.WhenTest w) {
		//Can't use '.map' because of the ref parameter
		var casesBuilder = w.cases.mapBuilder<Expr.WhenTest.Case>();
		for (uint i = 0; i < casesBuilder.Length; i++) {
			var kase = w.cases[i];
			var test = checkSubtype(BuiltinClass.Bool, kase.test);
			var result = checkExpr(ref expected, kase.result);
			casesBuilder[i] = new Expr.WhenTest.Case(kase.loc, test, result);
		}
		var cases = new Arr<Expr.WhenTest.Case>(casesBuilder);

		var elseResult = checkExpr(ref expected, w.elseResult);

		return new Expr.WhenTest(w.loc, cases, elseResult, expected.inferredType);
	}

	Expr checkAssert(ref Expected expected, Ast.Expr.Assert a) =>
		handle(ref expected, new Expr.Assert(a.loc, checkSubtype(BuiltinClass.Bool, a.asserted)));

	Expr checkTry(ref Expected expected, Ast.Expr.Try t) {
		var doo = checkExpr(ref expected, t._do);
		var katch = t._catch.get(out var c) ? Op.Some(checkCatch(ref expected, c)) : Op<Expr.Try.Catch>.None;
		var finallee = t._finally.get(out var f) ? Op.Some(checkVoid(f)) : Op<Expr>.None;
		return new Expr.Try(t.loc, doo, katch, finallee, expected.inferredType);
	}

	Expr.Try.Catch checkCatch(ref Expected expected, Ast.Expr.Try.Catch c) {
		var exceptionTy = baseScope.getTy(c.exceptionTy);
		var caught = new Pattern.Single(c.exceptionNameLoc, exceptionTy, c.exceptionName);
		addToScope(caught);
		var then = checkExpr(ref expected, c.then);
		popFromScope();
		return new Expr.Try.Catch(c.loc, caught, then);
	}

	Expr callStaticMethod(ref Expected expected, Loc loc, ClassLike klass, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!klass.membersMap.get(methodName, out var member)) TODO();
		var method = (Method)member;
		if (!method.isStatic) throw TODO();
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new Expr.StaticMethodCall(loc, method, args));
	}

	Expr callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Expr> argAsts) {
		var target = checkInfer(targetAst);
		var member = getMember(loc, target.ty, methodName);
		var method = (Method)member; //TODO: error handling
		if (method.isStatic) throw TODO(); //error
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new Expr.InstanceMethodCall(loc, target, method, args));
	}

	Expr callOwnMethod(ref Expected expected, Loc loc, Sym methodName, Arr<Ast.Expr> argAsts) {
		var member = getMember(loc, currentClass, methodName);
		var method = (Method)member; //TODO: error handling
		var args = checkCall(loc, method, argAsts);
		var call = method.isStatic ? new Expr.StaticMethodCall(loc, method, args) : (Expr)new Expr.MyInstanceMethodCall(loc, method, args);
		return handle(ref expected, call);
	}

	static Member getMember(Loc loc, Ty ty, Sym memberName) {
		if (tryGetMember(ty, memberName, out var member))
			return member;

		unused(loc);
		throw TODO(); //error
	}

	static bool tryGetMember(Ty ty, Sym memberName, out Member member) {
		var klass = (ClassLike)ty; //TODO
		if (klass.membersMap.get(memberName, out member)) {
			return true;
		}

		foreach (var super in klass.supers) {
			if (tryGetMember(super.superClass, memberName, out member))
				return true;
		}

		return false;
	}

	Arr<Expr> checkCall(Loc callLoc, Method method, Arr<Ast.Expr> argAsts) {
		if (!effect.contains(method.effect))
			throw TODO(); // Error: can't call method with greater effect
		return checkCallArguments(callLoc, method.parameters, argAsts);
	}

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
		if (parameters.find(out var param, p => p.name == name))
			return new Expr.AccessParameter(loc, param);

		if (locals.find(out var local, l => l.name == name))
			return new Expr.AccessLocal(loc, local);

		if (!baseScope.tryGetMember(name, out var member))
			throw TODO(); //error: cannot find name...

		switch (member) {
			case Slot slot:
				if (isStatic) throw TODO();
				return new Expr.GetMySlot(loc, currentClass, slot);

			case Method.MethodWithBody m:
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
		Expected(Kind kind, Ty ty) { this.kind = kind; this.expectedTy = Op.fromNullable(ty); }

		internal bool inTailCallPosition => kind == Kind.Return;

		internal static Expected Return(Ty ty) => new Expected(Kind.Return, ty);
		internal static Expected SubTypeOf(Ty ty) => new Expected(Kind.SubTypeOf, ty);
		internal static Expected Infer() => new Expected(Kind.Infer, null);

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
						// Types must exactly equal.
						if (ety != e.ty) {
							//loc;
							//new Err.CombineTypes(ety, e.ty);
							throw TODO();
						}
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

	//mv
	Expr checkType(Ty expectedTy, Expr e) {
		//TODO: subtyping!
		if (isSubtype(expectedTy, e.ty))
			return e;

		//TODO: have a WrongCast node and issue a diagnostic.
		unused(this);
		throw TODO();
	}

	bool isSubtype(Ty expectedTy, Ty actualTy) {
		if (expectedTy.fastEquals(actualTy))
			return true;
		foreach (var s in actualTy.supers) {
			if (isSubtype(expectedTy, s.superClass))
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
