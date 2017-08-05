using Diag.ParseDiags;
using static ParserExitException;
using static Utils;

abstract class ExprParser : TyParser {
	protected ExprParser(string source) : base(source) {}

	protected Ast.Expr parseBlock() {
		var start = pos;
		var next = nextToken();
		return parseBlockWithStart(start, next);
	}

	enum Ctx {
		Statement,
		/** Allow any operator */
		YesOperators,
		/** Like Plain, but stop if you see an operator. */
		NoOperator,
	}

	Ast.Expr parseBlockWithStart(Pos start, Token first) {
		var (expr, next) = parseExpr(Ctx.Statement, start, first);
		switch (next) {
			case Token.Newline: {
				if (expr is Ast.Let l) {
					l.then = parseBlock();
					return l;
				} else {
					var rest = parseBlock();
					return new Ast.Seq(locFrom(start), expr, rest);
				}
			}
			case Token.Dedent:
				return expr;
			case Token.ParenR:
			case Token.Indent:
				throw TODO(); //unexpected
			default:
				throw unreachable();
		}
	}


	Ast.Expr parseExprAndExpectNext(Ctx ctx, Token expectedNext) =>
		parseExprAndExpectNext(ctx, expectedNext, pos, nextToken());

	Ast.Expr parseExprAndExpectNext(Ctx ctx, Token expectedNext, Pos start, Token startToken) {
		var (expr, next) = parseExpr(ctx, start, startToken);
		if (next != expectedNext) throw TODO();
		return expr;
	}

	(Ast.Expr, Token next) parseExpr(Ctx ctx) => parseExpr(ctx, pos, nextToken());

	(Ast.Expr, Token next) parseExpr(Ctx ctx, Pos start, Token firstToken) {
		var (first, next) = parseFirstExpr(start, firstToken);
		switch (next) {
			case Token.BracketL: {
				var typeArguments = takeTypeArgumentsAfterPassingLbracket();
				if (tryTakeColon())
					// `f[Nat]: g 1, 2` should be parsed like `f<Nat>(g(1, 2))`.
					// Base it on the Token.Colon case below.
					throw TODO();
				else {
					takeSpace();

					var (args, next2) = parseArgs(Ctx.NoOperator);
					var call = new Ast.Call(locFrom(start), first, typeArguments, args);
					return ctx != Ctx.NoOperator && next2 == Token.Operator ? slurpOperators(start, call) : (call, next2);
				}
			}

			case Token.Colon: {
				if (ctx == Ctx.NoOperator)
					throw TODO();

				takeSpace();
				var (args, next2) = parseArgs(Ctx.YesOperators);
				var call = new Ast.Call(locFrom(start), first, Arr.empty<Ast.Ty>(), args);
				return (call, next2);
			}

			case Token.Operator:
				// In `f x + 1`, we would have read through the space while parsing the arguments to `f`. So can encounter an operator now.
				return ctx == Ctx.NoOperator ? (first, Token.Operator) : slurpOperators(start, first);

			case Token.Space: {
				var nextStart = pos;
				var tok = nextToken();
				switch (tok) {
					case Token.Colon: {
						if (ctx != Ctx.Statement)
							throw TODO(); //diagnostic -- can't use `:=` in expression, did you mean `: ` or `==`?
						takeEquals();
						if (!(first is Ast.Access access))
							throw exit(first.loc, PrecedingEquals.instance);
						var propertyName = access.name;
						takeSpace();
						var (value, next2) = parseExpr(Ctx.YesOperators);
						return (new Ast.SetProperty(locFrom(start), propertyName, value), next2);
					}

					case Token.Equals: {
						if (ctx != Ctx.Statement)
							throw TODO(); //diagnostic -- can't use `=` in expression, did you mean `:=`?
						if (!(first is Ast.Access access))
							throw exit(first.loc, PrecedingEquals.instance);
						var pattern = new Ast.Pattern.Single(access.loc, access.name);
						takeSpace();
						var (value, next2) = parseExpr(Ctx.YesOperators);
						if (next2 != Token.Newline)
							throw exit(locFrom(start), BlockCantEndInLet.instance);
						return (new Ast.Let(locFrom(start), pattern, value), Token.Newline);
					}

					case Token.Then:
					case Token.Else:
						return (first, tok == Token.Then ? Token.Then : Token.Else);

					case Token.Operator:
						// If we are already on the RHS of an operator, don't continue parsing operators -- leave that to the outer version of `parseExprWithNext`.
						// This ensures that `a + b * c` is parsed as `(a + b) * c`, because we stop parsing at the `*` and allow the outer parser to continue.
						return ctx == Ctx.NoOperator ? (first, Token.Operator) : slurpOperators(start, first);

					default: {
						var (args, next2) = parseArgs(Ctx.NoOperator, nextStart, tok);
						var call = new Ast.Call(locFrom(start), first, Arr.empty<Ast.Ty>(), args);
						return ctx != Ctx.NoOperator && next2 == Token.Operator ? slurpOperators(start, call) : (call, next2);
					}
				}
			}

			default:
				return (first, next);
		}
	}

	// Only meant to be called from 'parseExpr'.
	(Ast.Expr, Token next) parseFirstExpr(Pos start, Token token) {
		switch (token) {
			case Token.New: {
				// e.g. `new[Nat] 1, 2`
				var ctx = tryTakeColon() ? Ctx.YesOperators : Ctx.NoOperator;
				var typeArguments = tryTakeTypeArguments();
				takeSpace();
				var (args, next) = parseArgs(ctx);
				var expr = new Ast.New(locFrom(start), typeArguments, args);
				return (expr, next);
			}

			case Token.Recur: {
				var ctx = tryTakeColon() ? Ctx.YesOperators : Ctx.NoOperator;
				takeSpace();
				var (args, next) = parseArgs(ctx);
				var expr = new Ast.Recur(locFrom(start), args).upcast<Ast.Expr>();
				return (expr, next);
			}

			case Token.Assert: {
				takeSpace();
				var (asserted, next) = parseExpr(Ctx.YesOperators);
				var assert = new Ast.Assert(locFrom(start), asserted);
				return (assert, next);
			}

			case Token.If: {
				takeSpace();
				var test = parseExprAndExpectNext(Ctx.YesOperators, Token.Then);
				takeSpace();
				var then = parseExprAndExpectNext(Ctx.YesOperators, Token.Else);
				takeSpace();
				var (@else, next) = parseExpr(Ctx.YesOperators);
				var ifElse = new Ast.IfElse(locFrom(start), test, then, @else);
				return (ifElse, next);
			}

			case Token.When:
			case Token.Try: {
				var expr = token == Token.When ? parseWhen(start) : parseTry(start);
				var next = tryTakeDedentFromDedenting() ? Token.Dedent : Token.Newline;
				return (expr, next);
			}

			default:
				return parseSimpleExpr(start, token);
		}
	}

	(Ast.Expr, Token next) slurpOperators(Pos start, Ast.Expr first) {
		var @operator = tokenSym;

		var left = first;
		while (true) {
			takeSpace(); // operator must be followed by space.
			var (right, next2) = parseExpr(Ctx.NoOperator);
			left = new Ast.OperatorCall(locFrom(start), left, @operator, right);
			if (next2 == Token.Operator)
				@operator = tokenSym;
			else
				return (left, next2);
		}
	}

	(Arr<Ast.Expr>, Token next) parseArgs(Ctx ctx) => parseArgs(ctx, pos, nextToken());

	(Arr<Ast.Expr>, Token next) parseArgs(Ctx ctx, Pos start, Token firstToken) {
		//TODO:helper fn
		var parts = Arr.builder<Ast.Expr>();
		var (firstArg, next) = parseExpr(ctx, start, firstToken);
		parts.add(firstArg);
		while (next == Token.Comma) {
			takeSpace();
			var (nextArg, nextNext) = parseExpr(ctx, pos, nextToken());
			parts.add(nextArg);
			next = nextNext;
		}
		return (parts.finish(), next);
	}

	(Ast.Expr, Token) parseSimpleExpr(Pos pos, Token token) {
		var expr = parseSimpleExprWithoutSuffixes(pos, token);

		while (true) {
			var start = pos;
			var next = nextToken();
			switch (next) {
				case Token.Dot:
					var name = takeName();
					expr = new Ast.GetProperty(locFrom(start), expr, name);
					break;

				case Token.ParenL:
					takeRparen();
					expr = new Ast.Call(locFrom(start), expr, Arr.empty<Ast.Ty>(), Arr.empty<Ast.Expr>());
					break;

				default:
					return (expr, next);
			}
		}
	}

	Ast.Expr parseSimpleExprWithoutSuffixes(Pos pos, Token token) {
		switch (token) {
			case Token.TyName: {
				var className = tokenSym;
				takeDot();
				var staticMethodName = takeName();
				return new Ast.StaticAccess(locFrom(pos), className, staticMethodName);
			}
			case Token.ParenL:
				return parseExprAndExpectNext(Ctx.YesOperators, Token.ParenR);
			default:
				return singleTokenExpr(locFrom(pos), token);
		}
	}

	Ast.Expr singleTokenExpr(Loc loc, Token token) {
		switch (token) {
			case Token.Name:
				return new Ast.Access(loc, tokenSym);
			case Token.NatLiteral:
				return new Ast.Literal(loc, LiteralValue.Nat.of(uint.Parse(tokenValue)));
			case Token.IntLiteral:
				return new Ast.Literal(loc, LiteralValue.Int.of(int.Parse(tokenValue)));
			case Token.RealLiteral:
				return new Ast.Literal(loc, LiteralValue.Real.of(double.Parse(tokenValue)));
			case Token.StringLiteral:
				return new Ast.Literal(loc, LiteralValue.String.of(tokenValue));
			case Token.Pass:
				return new Ast.Literal(loc, LiteralValue.Pass.instance);
			case Token.True:
			case Token.False:
				return new Ast.Literal(loc, LiteralValue.Bool.of(token == Token.True));
			case Token.Self:
				return new Ast.Self(loc);
			default:
				throw TODO(); //diagnostic
		}
	}

	Ast.Expr parseWhen(Pos startPos) {
		/*
		when
			firstTest
				firstResult
			else
				elseResult
		*/
		takeIndent();

		var cases = Arr.builder<Ast.WhenTest.Case>();

		var caseStart = startPos;
		var caseStartToken = nextToken();
		do {
			var firstTest = parseExprAndExpectNext(Ctx.YesOperators, Token.Indent, caseStart, caseStartToken);
			var firstResult = parseBlock();
			cases.add(new Ast.WhenTest.Case(locFrom(caseStart), firstTest, firstResult));

			caseStart = pos;
			caseStartToken = nextToken();
		} while (caseStartToken != Token.Else);

		takeIndent();
		var elseResult = parseBlock();
		if (!tryTakeDedentFromDedenting()) {
			throw TODO(); //'else' must be the last clause. Must double-dedent after its block.
		}

		return new Ast.WhenTest(locFrom(startPos), cases.finish(), elseResult);
	}

	Ast.Expr parseTry(Pos startPos) {
		/*
		try
			do
				...
			catch Exception e | optional
				...
			else | optional
				...
			finally | optional
				...
		*/
		takeIndent();
		takeSpecificKeyword("do");
		takeIndent();
		var do_ = parseBlock();

		var catch_ = Op<Ast.Try.Catch>.None;
		var finally_ = Op<Ast.Expr>.None;

		var catchStart = pos;

		switch (takeCatchOrFinally()) {
			case CatchOrFinally.Catch: {
				takeSpace();
				var exceptionType = parseTy();
				takeSpace();
				var nameStart = pos;
				var exceptionName = takeName();
				var nameLoc = locFrom(nameStart);
				takeIndent();
				var catchBlock = parseBlock();
				catch_ = Op.Some(new Ast.Try.Catch(locFrom(catchStart), exceptionType, nameLoc, exceptionName, catchBlock));

				if (tryTakeDedent())
					break;
				else {
					takeSpecificKeyword("finally");
					goto case CatchOrFinally.Finally;
				}
			}

			case CatchOrFinally.Finally: {
				takeIndent();
				finally_ = Op.Some(parseBlock());
				takeDedent();
				break;
			}

			default: throw unreachable();
		}

		return new Ast.Try(locFrom(startPos), do_, catch_, finally_);
	}
}
