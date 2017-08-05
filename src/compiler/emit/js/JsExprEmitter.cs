using Model;

using static EstreeUtils;
using static NameEscaping;
using static Utils;

sealed class JsExprEmitter {
	internal bool needsLib = false;
	readonly Sym currentClassName;
	readonly bool thisMethodIsAsync;

	internal static Estree.BlockStatement emitMethodBody(ref bool needsLib, Sym className, bool @async, Expr body) {
		var ee = new JsExprEmitter(className, @async);
		var res = ee.exprToBlockStatement(body);
		if (ee.needsLib) needsLib = true;
		return res;
	}

	JsExprEmitter(Sym currentClassName, bool thisMethodIsAsync) {
		this.currentClassName = currentClassName;
		this.thisMethodIsAsync = thisMethodIsAsync;
	}

	Estree.BlockStatement exprToBlockStatement(Expr expr) =>
		new Estree.BlockStatement(expr.loc, writeExprToBlockStatement(expr).finish());

	Estree.BlockStatement exprToBlockStatement(Expr expr, Estree.Statement firstStatement) =>
		new Estree.BlockStatement(expr.loc, writeExprToBlockStatement(expr).finishWithFirst(firstStatement));

	Arr.Builder<Estree.Statement> writeExprToBlockStatement(Expr expr) {
		var parts = Arr.builder<Estree.Statement>();
		while (true) {
			var loc = expr.loc;
			switch (expr) {
				case Let let: {
					var (_, assigned, value, then) = let;
					var x = (Pattern.Single)assigned; // TODO: handle other patterns
					parts.add(Estree.VariableDeclaration.simple(loc, id(x.loc, escapeName(x.name)), exprToExpr(value)));
					expr = then;
					break;
				}
				case Seq seq: {
					var (_, action, then) = seq;
					parts.add(exprToStatementWorker(action, needReturn: false));
					expr = then;
					break;
				}
				case Literal l:
					if (l.value is LiteralValue.Pass)
						return parts;
					goto default;
				default:
					parts.add(exprToStatementWorker(expr, needReturn: true));
					return parts;
			}
		}
	}

	Estree.Statement exprToReturnStatementOrBlock(Expr expr) {
		switch (expr) {
			case Let _:
			case Seq _:
				return exprToBlockStatement(expr);
			case Literal l:
				if (l.value is LiteralValue.Pass)
					return new Estree.BlockStatement(l.loc, Arr.empty<Estree.Statement>());
				goto default;
			default:
				return exprToStatementWorker(expr, needReturn: true);
		}
	}

	// e.g. `return f(1)` or `if (so) return 1; else return 2;`
	Estree.Statement exprToStatementWorker(Expr expr, bool needReturn) {
		switch (expr) {
			case Assert a:
				return assertToStatement(a);
			case IfElse i:
				return ifElseToStatement(i);
			case WhenTest w:
				return whenToStatement(w);
			case Try t:
				return tryToStatement(t);
			default:
				var e = exprToExpr(expr);
				return needReturn ? Estree.ReturnStatement.of(e) : (Estree.Statement)Estree.ExpressionStatement.of(e);
		}
	}

	Estree.Statement assertToStatement(Assert a) {
		var (loc, asserted) = a;
		needsLib = true;
		var idAssertionException = JsBuiltins.getAssertionException(loc);
		var fail = new Estree.ThrowStatement(loc,
			new Estree.NewExpression(loc, idAssertionException, Arr.of<Estree.Expression>()));
		var notAsserted = negate(exprToExpr(asserted));
		return new Estree.IfStatement(loc, notAsserted, fail);
	}

	static Dict<string, string> inverseOperators = Dict.of(
		("===", "!=="),
		("!==", "==="),
		("<", ">="),
		("<=", ">"),
		(">=", "<"),
		(">", "<="));
	static Estree.Expression negate(Estree.Expression e) {
		switch (e) {
			case Estree.BinaryExpression b:
				if (inverseOperators.get(b.@operator, out var inverse))
					return new Estree.BinaryExpression(b.loc, "!==", b.left, b.right);
				goto default;
			case Estree.UnaryExpression u:
				if (u.@operator == "!")
					return u.argument;
				goto default;
			default:
				return Estree.UnaryExpression.not(e.loc, e);
		}
	}

	Estree.Statement tryToStatement(Try @try) {
		var (loc, @do, @catch, @finally) = @try;
		var jsDo = exprToBlockStatement(@do);
		var jsCatch = @catch.get(out var c) ? Op.Some(catchToCatch(c)) : Op<Estree.CatchClause>.None;
		var jsFinally = @finally.get(out var f) ? Op.Some(exprToBlockStatement(f)) : Op<Estree.BlockStatement>.None;
		return new Estree.TryStatement(loc, jsDo, jsCatch, jsFinally);
	}

	Estree.CatchClause catchToCatch(Try.Catch c) {
		var loc = c.loc;
		var caught = id(c.caught.loc, escapeName(c.caught.name));

		// `if (!(e is ExceptionType)) throw e;`
		var isInstance = new Estree.BinaryExpression(loc, "instanceof", caught, accessClass(loc, ((PlainTy)c.exceptionTy).instantiatedClass));
		var test = Estree.UnaryExpression.not(loc, isInstance);
		var first = new Estree.IfStatement(loc, test, new Estree.ThrowStatement(c.loc, caught));

		var caughtBlock = exprToBlockStatement(c.then, first);
		return new Estree.CatchClause(loc, caught, caughtBlock);
	}

	Estree.Expression accessClass(Loc loc, InstCls cls) {
		switch (cls.classDeclaration) {
			case ClassDeclaration k:
				return id(loc, escapeName(k.name));
			case BuiltinClass b:
				needsLib = true;
				return JsBuiltins.getBuiltin(loc, b);
			default:
				throw TODO();
		}
	}

	Estree.Expression iife(Expr expr) {
		var fn = new Estree.ArrowFunctionExpression(expr.loc, thisMethodIsAsync, Arr.empty<Estree.Pattern>(), exprToBlockStatement(expr));
		return callPossiblyAsync(expr.loc, thisMethodIsAsync, fn, Arr.empty<Estree.Expression>());
	}

	Estree.Expression exprToExpr(Expr expr) {
		switch (expr) {
			case AccessParameter p: {
				var (loc, param) = p;
				return id(loc, escapeName(param.name));
			}
			case AccessLocal lo: {
				var (loc, local) = lo;
				return id(loc, escapeName(local.name));
			}
			case Let _:
			case Seq _:
			case Assert _:
			case Try _:
				return iife(expr);
			case Literal li:
				return new Estree.Literal(li.loc, li.value);
			case StaticMethodCall sm:
				return emitStaticMethodCall(sm);
			case InstanceMethodCall m:
				return emitInstanceMethodCall(m);
			case MyInstanceMethodCall my:
				return emitMyInstanceMethodCall(my);
			case New n: {
				var (loc, slots, /*tyArgs*/_, args) = n;
				return new Estree.NewExpression(loc, id(loc, escapeName(slots.klass.name)), args.map(exprToExpr));
			}
			case Recur r:
				return emitRecur(r);
			case GetSlot g: {
				var (loc, target, slot) = g;
				return Estree.MemberExpression.simple(loc, exprToExpr(target), escapeName(slot.name));
			}
			case SetSlot s: {
				var (loc, slot, value) = s;
				var thisDotSlot = Estree.MemberExpression.simple(loc, new Estree.ThisExpression(loc), slot.name.str);
				return new Estree.AssignmentExpression(loc, thisDotSlot, exprToExpr(value));
			}
			case GetMySlot g: {
				var (loc, slot) = g;
				return Estree.MemberExpression.simple(loc, new Estree.ThisExpression(loc), escapeName(slot.name));
			}
			case IfElse i:
				return ifElseToExpr(i);
			case WhenTest w:
				return whenToExpr(w);
			default:
				throw TODO();
		}
	}

	Estree.Expression emitStaticMethodCall(StaticMethodCall stat) {
		var (loc, method, args) = stat;
		return JsBuiltins.emitStaticMethodCall(ref needsLib, method.decl, loc, args.map(exprToExpr));
	}

	Estree.Expression emitInstanceMethodCall(InstanceMethodCall instance) {
		var (loc, target, method, args) = instance;
		return JsBuiltins.emitInstanceMethodCall(ref needsLib, method.decl, loc, exprToExpr(target), args.map(exprToExpr));
	}

	Estree.Expression emitMyInstanceMethodCall(MyInstanceMethodCall myInstance) {
		var (loc, method, args) = myInstance;
		return JsBuiltins.emitMyInstanceMethodCall(ref needsLib, method.decl, loc, args.map(exprToExpr));
	}

	Estree.Expression emitRecur(Recur r) {
		var (loc, recurseTo, args) = r;
		var implemented = recurseTo.implementedMethod;
		var methodName = implemented.name;
		var fn = implemented.isStatic
			? Estree.MemberExpression.simple(loc, escapeName(currentClassName), escapeName(methodName))
			: Estree.MemberExpression.ofThis(loc, escapeName(methodName));
		return new Estree.CallExpression(loc, fn, args.map(exprToExpr));
	}

	Estree.Statement ifElseToStatement(IfElse ifElse) {
		var (loc, test, then, @else) = ifElse;
		return new Estree.IfStatement(loc, exprToExpr(test), exprToReturnStatementOrBlock(then), exprToReturnStatementOrBlock(@else));
	}

	Estree.Expression ifElseToExpr(IfElse ifElse) {
		var (loc, test, then, @else) = ifElse;
		return new Estree.ConditionalExpression(loc, exprToExpr(test), exprToExpr(then), exprToExpr(@else));
	}

	/** if (test1) { return result1; } else if (test2) { return result2; } else { return result3; } */
	Estree.Statement whenToStatement(WhenTest whenTest) {
		var (_, cases, elseResult) = whenTest;
		return cases.foldBackwards(exprToReturnStatementOrBlock(elseResult), (res, kase) =>
			new Estree.IfStatement(kase.loc, exprToExpr(kase.test), exprToReturnStatementOrBlock(kase.result), res));
	}

	/** test1 ? result1 : test2 ? result2 : elze */
	Estree.Expression whenToExpr(WhenTest whenTest) {
		var (_, cases, elseResult) = whenTest;
		return cases.foldBackwards(exprToExpr(elseResult), (res, kase) =>
			new Estree.ConditionalExpression(kase.loc,  exprToExpr(kase.test), exprToExpr(kase.result), res));
	}
}
