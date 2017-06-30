using System;
using System.Collections.Generic;

using Model;
using static Utils;

struct BaseScope {
	internal readonly Klass self;
	readonly Arr<Module> imports;

	internal bool hasMember(Sym name) =>
		self.membersMap.has(name);

	internal bool tryGetMember(Sym name, out Member member) =>
		self.membersMap.get(name, out member);

	internal BaseScope(Klass self, Arr<Module> imports) {
		this.self = self; this.imports = imports;
		for (uint i = 0; i < imports.length; i++) {
			var import = imports[i];
			if (import.name == self.name)
				throw TODO(); // diagnostic -- can't shadow self
			for (uint j = 0; j < i; j++)
				if (imports[j].name == import.name)
					throw TODO(); // diagnostic -- can't shadow another import
		}
	}

	internal Ty getTy(Ast.Ty ast) {
		switch (ast) {
			case Ast.Ty.Access access:
				return accessTy(access.loc, access.name);
			case Ast.Ty.Inst inst:
				var a = accessTy(inst.instantiated.loc, inst.instantiated.name);
				var b = inst.tyArgs.map(getTy);
				unused(a, b);
				throw TODO(); //TODO: type instantiation
			default:
				throw unreachable();
		}
	}

	internal Ty accessTy(Loc loc, Sym name) {
		if (name == self.name)
			return self;

		if (imports.find(out var found, i => i.name == name))
			return found.klass;

		if (BuiltinClass.tryGet(name, out var builtin))
			return builtin;

		unused(loc);
		throw TODO(); //Return dummy Ty and issue diagnostic
	}
}

class Checker {
	internal static Klass checkClass(Module module, Arr<Module> imports, Ast.Klass ast, Sym name) {
		var klass = new Klass(module, ast.loc, name);
		return new Checker(klass, imports).checkClass(klass, ast);
	}

	readonly BaseScope baseScope;

	Checker(Klass klass, Arr<Module> imports) {
		this.baseScope = new BaseScope(klass, imports);
	}

	//neater
	//TODO: also handle abstract methods in superclass of superclass
	static Arr<Method.AbstractMethod> getAbstractMethods(Klass superClass) {
		if (!(superClass.head is Klass.Head.Abstract abs))
			throw TODO();
		return abs.abstractMethods;
	}

	Klass checkClass(Klass klass, Ast.Klass ast) {
		var b = Dict.builder<Sym, Member>();
		void addMember(Member member) {
			if (!b.TryAdd(member.name, member))
				throw TODO(); //diagnostic
		}

		var methods = ast.methods.map(methodAst => {
			var e = emptyMethod(klass, methodAst);
			addMember(e);
			return e;
		});
		klass.methods = methods;

		// Adds slot
		klass.head = checkHead(klass, ast.head, addMember, methods);

		klass.setMembersMap(new Dict<Sym, Member>(b));

		// Not that all members exist, we can fill in bodies.
		klass.setSupers(ast.supers.map(superAst => {
			var superClass = (Klass)baseScope.accessTy(superAst.loc, superAst.name); //TODO: handle builtin
			var super = new Super(superAst.loc, klass, superClass);

			var abstractMethods = getAbstractMethods((Klass)super.superClass); //TODO: handle other kinds of superClass

			var implAsts = superAst.impls;

			if (implAsts.length != abstractMethods.length)
				throw TODO(); // Something wasn't implemented.

			for (uint i = 0; i < implAsts.length; i++) {
				//Can't implement the same method twice (TEST)
				var name = implAsts[i].name;
				for (uint j = 0; j < i; j++) {
					if (implAsts[j].name == name)
						throw TODO(); // duplicate implementation
				}
			}

			super.impls = implAsts.map(implAst => {
				var name = implAst.name;
				if (!abstractMethods.find(out var implemented, a => a.name == name))
					throw TODO(); // Implemented a non-existent method.

				if (implAst.parameters.length != implemented.parameters.length)
					throw TODO(); // Too many / not enough parameters
				implAst.parameters.doZip(implemented.parameters, (implParameter, abstractParameter) => {
					if (implParameter != abstractParameter.name)
						throw TODO(); // Parameter names don't match
				});

				var body = MethodChecker.checkMethod(baseScope, false, implemented.returnTy, implemented.parameters, implAst.body);
				return new Impl(super, implAst.loc, implemented, body);
			});

			return super;
		}));

		// Now that all members exist, fill in the body of each member.
		ast.methods.doZip(methods, (memberAst, member) => {
			switch (memberAst) {
				case Ast.Member.Method methodAst:
					var method = (Method.MethodWithBody)member;
					method.body = MethodChecker.checkMethod(baseScope, method.isStatic, method.returnTy, method.parameters, methodAst.body);
					break;
				case Ast.Member.AbstractMethod _:
					break;
				default:
					throw unreachable();
			}
		});

		return klass;
	}

	Klass.Head checkHead(Klass klass, Ast.Klass.Head ast, Action<Member> addMember, Arr<Method> methods) {
		var loc = ast.loc;
		switch (ast) {
			case Ast.Klass.Head.Static _:
				return new Klass.Head.Static(loc);

			case Ast.Klass.Head.Abstract _: {
				var abstractMethods = methods.keepOfType<Method.AbstractMethod>();
				return new Klass.Head.Abstract(loc, abstractMethods);
			}

			case Ast.Klass.Head.Slots slotsAst: {
				var slots = new Klass.Head.Slots(loc, klass);
				slots.slots = slotsAst.slots.map(var => {
					var slot = new Slot(slots, ast.loc, var.mutable, baseScope.getTy(var.ty), var.name);
					addMember(slot);
					return slot;
				});
				return slots;
			}

			default:
				throw unreachable();
		}
	}

	Method emptyMethod(Klass klass, Ast.Member ast) {
		switch (ast) {
			case Ast.Member.Method m:
				return new Method.MethodWithBody(
					klass, m.loc, m.isStatic, baseScope.getTy(m.returnTy), m.name, getParams(m.parameters));
			case Ast.Member.AbstractMethod a:
				return new Method.AbstractMethod(
					klass, a.loc, baseScope.getTy(a.returnTy), a.name, getParams(a.parameters));
			default:
				throw unreachable();
		}
	}

	Arr<Method.Parameter> getParams(Arr<Ast.Member.Parameter> pms) =>
		pms.map((p, index) =>
			new Method.Parameter(p.loc, baseScope.getTy(p.ty), p.name, index));
}

class MethodChecker {
	internal static Expr checkMethod(BaseScope baseScope, bool isStatic, Ty returnTy, Arr<Method.Parameter> parameters, Ast.Expr body) =>
		new MethodChecker(baseScope, isStatic, parameters).checkSubtype(returnTy, body);

	readonly BaseScope baseScope;
	Klass currentClass => baseScope.self;
	readonly bool isStatic;
	readonly Arr<Method.Parameter> parameters;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();

	MethodChecker(BaseScope baseScope, bool isStatic, Arr<Method.Parameter> parameters) {
		this.baseScope = baseScope;
		this.isStatic = isStatic;
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

	Expr checkVoid(Ast.Expr a) {
		var e = Expected.Void;
		return checkExpr(ref e, a);
	}

	Expr checkInfer(Ast.Expr a) {
		var e = Expected.Infer();
		return checkExpr(ref e, a);
	}

	Expr checkSubtype(Ty ty, Ast.Expr a) {
		var e = Expected.SubTypeOf(ty);
		return checkExpr(ref e, a);
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
				//Self-call.
				throw TODO();

			default:
				// Diagnostic -- can't call anything else.
				throw TODO();
		}
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
		getProperty(g.loc, g.target, g.propertyName, out var target, out var member);
		var slot = (Slot)member; //TODO
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
		if (expected.isVoid)
			//Need to test this here of expected.inferredType will fail
			throw TODO();

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
		handle(ref expected, new Expr.Assert(a.loc, checkSubtype(BuiltinClass.Void, a.asserted)));

	Expr callStaticMethod(ref Expected expected, Loc loc, ClassLike klass, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!klass.membersMap.get(methodName, out var member)) TODO();
		var method = (Method)member;
		if (!method.isStatic) throw TODO();
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new Expr.StaticMethodCall(loc, method, args));
	}

	Expr callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Expr> argAsts) {
		getProperty(loc, targetAst, methodName, out var target, out var member);
		var method = (Method)member; //TODO: error handling
		if (method.isStatic) throw TODO();
		var args = checkCall(loc, method, argAsts);
		return handle(ref expected, new Expr.InstanceMethodCall(loc, target, method, args));
	}

	void getProperty(Loc loc, Ast.Expr targetAst, Sym propertyName, out Expr target, out Member member) {
		target = checkInfer(targetAst);
		member = getMember(loc, target.ty, propertyName);
	}

	static Member getMember(Loc loc, Ty ty, Sym name) {
		var klass = (ClassLike)ty; //TODO: error handling
		if (!klass.membersMap.get(name, out var member)) {
			unused(loc);
			throw TODO();
		}
		return member;
	}

	Arr<Expr> checkCall(Loc callLoc, Method method, Arr<Ast.Expr> argAsts) {
		if (method.arity != argAsts.length) {
			unused(callLoc);
			throw TODO();
		}
		return method.parameters.zip(argAsts, (parameter, argAst) => checkSubtype(parameter.ty, argAst));
	}

	void addToScope(Pattern.Single local) {
		if (parameters.find(out var param, p => p.name == local.name))
			throw TODO(); //Illegal shadowing.
		if (locals.find(out var shadowedLocal, l => l.name == local.name))
			throw TODO(); //Illegal shadowing.
		locals.Push(local);
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
		enum Kind { Void, SubTypeOf, Infer }
		readonly Kind kind;
		//For Void, this is always null.
		//For SubTypeOf, this is always non-null.
		//For Infer, this is mutable.
		Op<Ty> expectedTy;
		Expected(Kind kind, Ty ty) { this.kind = kind; this.expectedTy = Op.fromNullable(ty); }

		internal bool isVoid => kind == Kind.Void;

		internal static Expected Void => new Expected(Kind.Void, null);
		internal static Expected SubTypeOf(Ty ty) => new Expected(Kind.SubTypeOf, ty);
		internal static Expected Infer() => new Expected(Kind.Infer, null);

		/** Note: This may be called on SubTypeOf. */
		internal Ty inferredType => expectedTy.force;

		internal Expr handle(Expr e, MethodChecker c) {
			switch (kind) {
				case Kind.Void:
					throw TODO(); //Diagnostic: expected void, got something else
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

	void endCheckPattern(uint nAdded) {
		doTimes(nAdded, () => locals.Pop());
	}
}
