using System;
using System.Collections.Generic;

using static Utils;
using Model;

struct BaseScope {
	internal readonly Klass self;
	readonly Arr<Module> imported;

	internal bool hasMember(Sym name) =>
		self.membersMap.has(name);

	internal bool tryGetMember(Sym name, out Member member) =>
		self.membersMap.get(name, out member);

	internal BaseScope(Klass self, Arr<Module> imported) {
		this.self = self; this.imported = imported;
		imported.each((import, i) => {
			if (import.name == self.name)
				throw TODO(); // diagnostic -- can't shadow self
			imported.eachInSlice(0, i, priorImport => {
				if (priorImport.name == import.name)
					throw TODO(); // diagnostic -- can't shadow another import
			});
		});
	}

	internal Ty getTy(Ast.Ty ast) {
		var access = ast as Ast.Ty.Access;
		if (access != null)
			return accessTy(access.loc, access.name);

		var inst = (Ast.Ty.Inst) ast;
		var a = accessTy(inst.instantiated.loc, inst.instantiated.name);
		var b = inst.tyArgs.map(getTy);
		throw TODO(); //TODO: type instantiation
	}

	internal Ty accessTy(Loc loc, Sym name) {
		if (name == self.name)
			return self;

		if (imported.find(out var found, i => i.name == name))
			return found.klass;

		if (BuiltinClass.tryGet(name, out var builtin))
			return builtin;

		throw TODO(); //Return dummy Ty and issue diagnostic
	}
}

class Checker {
	internal static Klass checkClass(Arr<Module> imported, Ast.Klass ast) {
		var klass = new Klass(ast.loc, ast.name);
		return new Checker(klass, imported).checkClass(klass, ast);
	}

	readonly BaseScope baseScope;

	Checker(Klass klass, Arr<Module> imported) {
		this.baseScope = new BaseScope(klass, imported);
	}

	Klass checkClass(Klass klass, Ast.Klass ast) {
		var b = Dict.builder<Sym, Member>();
		void addMember(Member member) {
			if (!b.TryAdd(member.name, member))
				throw TODO(); //diagnostic
		}

		klass.head = checkHead(klass, ast.head, addMember);

		var emptyMembers = ast.members.map(memberAst => {
			var e = emptyMember(klass, memberAst);
			addMember(e);
			return e;
		});
		klass.setMembersMap(new Dict<Sym, Member>(b));

		// Now that all members exist, fill in the body of each member.
		ast.members.doZip(emptyMembers, (memberAst, member) => {
			var methodAst = (Ast.Member.Method) memberAst; //TODO: other kinds
			var method = (MethodWithBody) member; //similarly, TODO
			method.body = MethodChecker.checkMethod(baseScope, method, methodAst.body);
		});

		return klass;
	}

	Klass.Head checkHead(Klass klass, Ast.Klass.Head ast, Action<Member> addMember) {
		var loc = ast.loc;
		var staticAst = ast as Ast.Klass.Head.Static;
		if (staticAst != null) {
			return new Klass.Head.Static(loc);
		}

		var slotsAst = ast as Ast.Klass.Head.Slots;
		if (slotsAst != null) {
			var slots = slotsAst.slots.map(var =>
				new Klass.Head.Slots.Slot(klass, ast.loc, var.mutable, baseScope.getTy(var.ty), var.name));
			slots.each(addMember);
			var head = new Klass.Head.Slots(loc, slots);
			return head;
		}

		throw TODO();
	}

	Member emptyMember(Klass klass, Ast.Member ast) {
		var mAst = (Ast.Member.Method) ast;
		var parameters = mAst.parameters.map(p =>
			new Method.Parameter(p.loc, baseScope.getTy(p.ty), p.name));
		return new MethodWithBody(
			klass, mAst.loc, mAst.isStatic, baseScope.getTy(mAst.returnTy), mAst.name, parameters);
	}
}

class MethodChecker {
	internal static Expr checkMethod(BaseScope baseScope, MethodWithBody method, Ast.Expr body) =>
		new MethodChecker(baseScope, method).checkSubtype(method.returnTy, body);

	readonly BaseScope baseScope;
	Klass currentClass => baseScope.self;
	readonly MethodWithBody currentMethod;
	Arr<MethodWithBody.Parameter> parameters => currentMethod.parameters;
	readonly Stack<Pattern.Single> locals = new Stack<Pattern.Single>();
	//TODO: Also readonly Push<Err> errors;

	MethodChecker(BaseScope baseScope, MethodWithBody method) {
		this.baseScope = baseScope;
		this.currentMethod = method;
		// Assert that parameters don't shadow members.
		parameters.each((param, i) => {
			if (baseScope.hasMember(param.name))
				throw TODO();
			parameters.eachInSlice(0, i, earlierParameter => {
				if (earlierParameter.name == param.name)
					throw TODO();
			});
		});
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
		var access = a as Ast.Expr.Access;
		if (access != null) return checkAccess(ref e, access);
		var sa = a as Ast.Expr.StaticAccess;
		if (sa != null) return checkStaticAccess(ref e, sa);
		var o = a as Ast.Expr.OperatorCall;
		if (o != null) return checkOperatorCall(ref e, o);
		var c = a as Ast.Expr.Call;
		if (c != null) return checkCallAst(ref e, c);
		var g = a as Ast.Expr.GetProperty;
		if (g != null) return checkGetProperty(ref e, g);
		var l = a as Ast.Expr.Let;
		if (l != null) return checkLet(ref e, l);
		var s = a as Ast.Expr.Seq;
		if (s != null) return checkSeq(ref e, s);
		var li = a as Ast.Expr.Literal;
		if (li != null) return checkLiteral(ref e, li);
		var w = a as Ast.Expr.WhenTest;
		if (w != null) return checkWhenTest(ref e, w);
		throw TODO();
	}

	Expr checkAccess(ref Expected expected, Ast.Expr.Access a) {
		var access = get(a.loc, a.name);
		return handle(ref expected, a.loc, access);
	}
	Expr checkStaticAccess(ref Expected expected, Ast.Expr.StaticAccess s) {
		//Not in a call, so create a callback
		throw TODO();
	}
	Expr checkOperatorCall(ref Expected expected, Ast.Expr.OperatorCall o) =>
		callMethod(ref expected, o.loc, o.left, o.oper, Arr.of(o.right));

	Expr checkCallAst(ref Expected expected, Ast.Expr.Call call) {
		var sa = call.target as Ast.Expr.StaticAccess;
		if (sa != null) {
			var ty = baseScope.accessTy(call.loc, sa.className);
			var klass = ty as ClassLike ?? throw TODO();
			return callStaticMethod(ref expected, sa.loc, klass, sa.staticMethodName, call.args);
		}

		var gp = call.target as Ast.Expr.GetProperty;
		if (gp != null)
			return callMethod(ref expected, gp.loc, gp.target, gp.propertyName, call.args);

		var ac = call.target as Ast.Expr.Access;
		if (ac != null) {
			//Self-call.
			throw TODO();
		}

		//Can't call anything else.
		throw TODO();
	}

	Expr checkGetProperty(ref Expected expected, Ast.Expr.GetProperty g) {
		getProperty(g.loc, g.target, g.propertyName, out var target, out var member);
		var slot = (Klass.Head.Slots.Slot) member; //TODO
		return handle(ref expected, g.loc, new Expr.GetSlot(g.loc, target, slot));
	}

	Expr checkLet(ref Expected expected, Ast.Expr.Let l) {
		Expr value = checkInfer(l.value);

		uint i = 0;
		var pattern = startCheckPattern(value.ty, l.assigned, ref i);
		var expr = checkExpr(ref expected, l.then);
		endCheckPattern(i);
		return new Expr.Let(l.loc, pattern, value, expr);
	}

	Expr checkSeq(ref Expected expected, Ast.Expr.Seq s) {
		var first = checkVoid(s.first);
		var then = checkExpr(ref expected, s.then);
		return new Expr.Seq(s.loc, first, then);
	}

	Expr checkLiteral(ref Expected expected, Ast.Expr.Literal l) =>
		handle(ref expected, l.loc, new Expr.Literal(l.loc, l.value));

	Expr checkWhenTest(ref Expected expected, Ast.Expr.WhenTest w) {
		var inferrer = Expected.Infer();

		var cases = w.cases.map(kase => {
			//kase.loc
			//kase.result
			//kase.test
			var test = checkSubtype(BuiltinClass.Bool, kase.test);
			var result = checkExpr(ref inferrer, kase.result);
			return new Expr.WhenTest.Case(kase.loc, test, result);
		});

		var elseResult = checkExpr(ref inferrer, w.elseResult);

		return new Expr.WhenTest(w.loc, cases, elseResult, inferrer.inferredType);
	}

	Expr callStaticMethod(ref Expected expected, Loc loc, ClassLike klass, Sym methodName, Arr<Ast.Expr> argAsts) {
		if (!klass.membersMap.get(methodName, out var member)) TODO();
		var method = (Method) member;
		if (method.isStatic) throw TODO();
		var args = checkCall(loc, method, argAsts);
		var call = new Expr.StaticMethodCall(loc, method, args);
		return handle(ref expected, loc, call);
	}

	Expr callMethod(ref Expected expected, Loc loc, Ast.Expr targetAst, Sym methodName, Arr<Ast.Expr> argAsts) {
		getProperty(loc, targetAst, methodName, out var target, out var member);
		var method = (Method) member; //TODO: error handling
		if (method.isStatic) throw TODO();
		var args = checkCall(loc, method, argAsts);
		var call = new Expr.MethodCall(loc, target, method, args);
		return handle(ref expected, loc, call);
	}

	void getProperty(Loc loc, Ast.Expr targetAst, Sym propertyName, out Expr target, out Member member) {
		target = checkInfer(targetAst);
		member = getMember(loc, target.ty, propertyName);
	}

	Member getMember(Loc loc, Ty ty, Sym name) {
		var klass = (ClassLike) ty;//TODO: error handling
		if (!klass.membersMap.get(name, out var member)) throw TODO();
		return member;
	}

	Arr<Expr> checkCall(Loc callLoc, Method method, Arr<Ast.Expr> argAsts) {
		if (method.arity != argAsts.length) {
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
			return new Expr.Access.Parameter(loc, param);

		if (locals.find(out var local, l => l.name == name))
			return new Expr.Access.Local(loc, local);

		if (!baseScope.tryGetMember(name, out var member))
			throw TODO(); //error: cannot find name...

		var slot = member as Klass.Head.Slots.Slot;
		if (slot != null) {
			if (currentMethod.isStatic) throw TODO();
			return new Expr.GetMySlot(loc, currentClass, slot);
		}

		var method = member as MethodWithBody;
		if (method != null) {
			throw TODO();
		}

		throw unreachable();
	}

	Expr handle(ref Expected expected, Loc loc, Expr e) =>
		expected.handle(loc, e, this);


	//TODO: struct passed by ref. struct Expected { Kind { Void, Subtype, Infer }; Kind kind; Ty ty; }

	/** PASS BY REF! */
	struct Expected {
		enum Kind { Void, SubTypeOf, Infer }
		readonly Kind kind;
		//For Void, this is always null.
		//For SubTypeOf, this is always non-null.
		//For Infer, this is mutable.
		Op<Ty> expectedTy;
		Expected(Kind kind, Ty ty) { this.kind = kind; this.expectedTy = Op.fromNullable(ty); }

		internal static Expected Void => new Expected(Kind.Void, null);
		internal static Expected SubTypeOf(Ty ty) => new Expected(Kind.SubTypeOf, ty);
		internal static Expected Infer() => new Expected(Kind.Infer, null);

		internal Ty inferredType => expectedTy.force;

		internal Expr handle(Loc loc, Expr e, MethodChecker c) {
			switch (kind) {
				case Kind.Void:
					throw TODO(); //Diagnostic: expected void, got something else
				case Kind.SubTypeOf:
					//Ty must be a subtype of this.
					return c.checkType(loc, expectedTy.force, e);
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
	Expr checkType(Loc loc, Ty expectedTy, Expr e) {
		//TODO: subtyping!
		if (expectedTy != e.ty) {
			//TODO: have a WrongCast node and issue a diagnostic.
			throw TODO();
		}
		return e;
	}

	/**
	 * MUST be used like so:
	 * int i = 0;
	 * var i = startCheckPattern(ty, pattern, ref i); // Adds things to scope
	 * ...do things with pattern variables in scope...
	 * endCheckPattern(i); // Removes them from scope
	 */
	Pattern startCheckPattern(Ty ty, Ast.Pattern ast, ref uint nAdded) {
		if (ast is Ast.Pattern.Ignore)
			return new Pattern.Ignore(ast.loc);

		var single = ast as Ast.Pattern.Single;
		if (single != null) {
			var s = new Pattern.Single(single.loc, ty, single.name);
			nAdded++;
			addToScope(s);
			return s;
		}

		var destruct = (Ast.Pattern.Destruct) ast;
		throw TODO();
	}

	void endCheckPattern(uint nAdded) {
		doTimes(nAdded, () => locals.Pop());
	}
}
