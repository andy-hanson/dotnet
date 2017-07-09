using static ParserExitException;
using static Utils;

abstract class ExprParser : Lexer {
	protected ExprParser(string source) : base(source) {}

	protected Ast.Ty parseTy() {
		var start = pos;
		var name = takeTyName();
		//TODO: handle Inst too
		return new Ast.Ty.Access(locFrom(start), name);
	}

	protected Ast.Expr parseBlock() {
		var start = pos;
		var next = nextToken();
		return parseBlockWithStart(start, next);
	}

	enum Ctx {
		/** Allow any operator */
		Plain,
		/** Like Plain, but stop if you see an operator. */
		NoOperator,
	}

	enum Next {
		NewlineAfterEquals, // This means that we got an incomplete Let expression. Must fill in the body.
		NewlineAfterStatement,
		NextOperator, //pair with NoOperator
		Dedent,
		Rparen,
		Indent,
		Comma,
	}

	Ast.Expr parseBlockWithStart(Pos start, Token first) {
		var (expr, next) = parseExprWithNext(Ctx.Plain, start, first);

		switch (next) {
			case Next.NewlineAfterEquals: {
				if (!(expr is Ast.Expr.Let l))
					throw unreachable();
				l.then = parseBlock();
				return l;
			}
			case Next.NewlineAfterStatement: {
				var rest = parseBlock();
				return new Ast.Expr.Seq(locFrom(start), expr, rest);
			}
			/*case Next.EndNestedBlock: {
				var start2 = pos;
				var first2 = nextToken();
				if (first2 == Token.Dedent)
					return expr;
				var rest = parseBlockWithStart(start2, first2);
				return new Ast.Expr.Seq(locFrom(start2), expr, rest);
			}*/
			case Next.Dedent:
				return expr;
			case Next.Rparen:
			case Next.Indent:
				throw TODO(); //unexpected
			default:
				throw unreachable();
		}
	}

	Ast.Expr parseExprAndEndContext(Ctx ctx, Next expectedNext) {
		var start = pos;
		var startToken = nextToken();
		var (expr, next) = parseExprWithNext(ctx, start, startToken);
		if (next != expectedNext) throw TODO();
		return expr;
	}

	(Ast.Expr, Next) parseExpr(Ctx ctx) {
		var start = pos;
		var startToken = nextToken();
		return parseExprWithNext(ctx, start, startToken);
	}

	enum SpecialStart { None, New, Recur }
	(Ast.Expr, Next) parseExprWithNext(Ctx ctx, Pos loopStart, Token loopNext) {
		var exprStart = loopStart;
		var specialStart = SpecialStart.None;
		var parts = Arr.builder<Ast.Expr>();

		switch (loopNext) {
			case Token.New:
			case Token.Recur:
				specialStart = loopNext == Token.New ? SpecialStart.New : SpecialStart.Recur;
				if (tryTakeColon())
					return parseAfterColon(parts, exprStart, specialStart);

				takeSpace();
				loopStart = pos;
				loopNext = nextToken();
				break;
			default:
				break;
		}


		while (true) {
			switch (loopNext) {
				case Token.Equals: {
					var patternLoc = locFrom(loopStart);
					var pattern = partsToPattern(patternLoc, parts);
					takeSpace();
					var (value, next) = parseExpr(Ctx.Plain);
					if (next != Next.NewlineAfterStatement) throw exit(locFrom(loopStart), Err.BlockCantEndInLet); //TODO: special error if next = Next.NewlineAfterEquals
					return (new Ast.Expr.Let(locFrom(loopStart), pattern, value), Next.NewlineAfterEquals);
				}

				/*
				a b + c * d
				*/
				case Token.Operator: {
					if (ctx == Ctx.NoOperator) {
						// If we are already on the RHS of an operator, don't continue parsing operators -- leave that to the outer version of `parseExprWithNext`.
						// This ensures that `a + b * c` is parsed as `(a + b) * c`, because we stop parsing at the `*` and allow the outer parser to continue.
						return (finishRegular(exprStart, specialStart, parts), Next.NextOperator);
					}

					var @operator = tokenSym;

					var left = finishRegular(exprStart, specialStart, parts);
					while (true) {
						takeSpace(); // operator must be followed by space.
						var (right, next) = parseExpr(Ctx.NoOperator);
						left = new Ast.Expr.OperatorCall(locFrom(exprStart), left, @operator, right);
						if (next == Next.NextOperator)
							@operator = tokenSym;
						else
							return (left, next);
					}
				}

				case Token.Assert: {
					if (!parts.isEmpty)
						throw TODO();
					takeSpace();
					var (asserted, next) = parseExpr(Ctx.Plain);
					var assert = new Ast.Expr.Assert(locFrom(exprStart), asserted);
					return (assert, next);
				}

				case Token.When:
				case Token.Try: {
					parts.add(loopNext == Token.When ? parseWhen(loopStart) : parseTry(loopStart));
					var expr = finishRegular(exprStart, specialStart, parts);
					var next = tryTakeDedentFromDedenting() ? Next.Dedent : Next.NewlineAfterStatement;
					return (expr, next);
				}

				default: {
					var (single, nextPos, tokenAfter) = parseSimpleExpr(loopStart, loopNext);
					parts.add(single);
					switch (tokenAfter) {
						case Token.Colon:
							return parseAfterColon(parts, exprStart, specialStart);

						case Token.Comma:
							takeSpace();
							return (single, Next.Comma);

						case Token.Space:
							loopStart = pos;
							loopNext = nextToken();
							// Continue adding parts
							break;

						case Token.Rparen:
							return (finishRegular(exprStart, specialStart, parts), Next.Rparen);

						case Token.Newline:
						case Token.Dedent: {
							var expr = finishRegular(exprStart, specialStart, parts);
							Next next;
							switch (tokenAfter) {
								case Token.Newline:
									next = Next.NewlineAfterStatement;
									break;
								case Token.Dedent:
									next = Next.Dedent;
									break;
								default:
									throw unreachable();
							}
							return (expr, next);
						}

						case Token.Indent:
							return (finishRegular(exprStart, specialStart, parts), Next.Indent);

						default:
							throw unexpected(nextPos, "Space or newline", tokenAfter);
					}

					break;
				}
			}
		}
	}

	(Ast.Expr, Next) parseAfterColon(Arr.Builder<Ast.Expr> parts, Pos exprStart, SpecialStart specialStart) {
		takeSpace();
		/*
		Take remaining parts differently.
		*/
		while (true) {
			var (e, next) = parseExpr(Ctx.Plain);
			parts.add(e);
			if (next != Next.Comma) {
				return (finishRegular(exprStart, specialStart, parts), next);
			}
		}
	}

	Ast.Expr finishRegular(Pos exprStart, SpecialStart specialStart, Arr.Builder<Ast.Expr> parts) {
		if (parts.curLength == 0)
			throw exit(singleCharLoc, Err.EmptyExpression);

		switch (specialStart) {
			case SpecialStart.None:
				var res = parts[0];
				if (parts.curLength > 1)
					res = new Ast.Expr.Call(locFrom(res.loc.start), res, parts.finishTail());
				return res;
			case SpecialStart.New:
				return new Ast.Expr.New(locFrom(exprStart), parts.finish());
			case SpecialStart.Recur:
				return new Ast.Expr.Recur(locFrom(exprStart), parts.finish());
			default:
				throw unreachable();
		}
	}

	(Ast.Expr, Pos, Token) parseSimpleExpr(Pos pos, Token token) {
		var expr = parseSimpleExprWithoutSuffixes(pos, token);

		while (true) {
			var start = pos;
			var next = nextToken();
			switch (next) {
				case Token.Dot:
					var name = takeName();
					expr = new Ast.Expr.GetProperty(locFrom(start), expr, name);
					break;

				case Token.Lparen:
					takeRparen();
					expr = new Ast.Expr.Call(locFrom(start), expr, Arr.empty<Ast.Expr>());
					break;

				default:
					return (expr, pos, next);
			}
		}
	}

	Ast.Expr parseSimpleExprWithoutSuffixes(Pos pos, Token token) {
		switch (token) {
			case Token.Name:
				return new Ast.Expr.Access(locFrom(pos), tokenSym);
			case Token.TyName: {
				var className = tokenSym;
				takeDot();
				var staticMethodName = takeName();
				return new Ast.Expr.StaticAccess(locFrom(pos), className, staticMethodName);
			}
			case Token.Lparen: {
				var (expr, next) = parseExpr(Ctx.Plain);
				if (next != Next.Rparen)
					throw TODO();
				return expr;
			}
			default:
				return singleTokenExpr(locFrom(pos), token);
		}
	}

	Ast.Expr singleTokenExpr(Loc loc, Token token) {
		switch (token) {
			case Token.NatLiteral:
				return new Ast.Expr.Literal(loc, LiteralValue.Nat.of(uint.Parse(tokenValue)));
			case Token.IntLiteral:
				return new Ast.Expr.Literal(loc, LiteralValue.Int.of(int.Parse(tokenValue)));
			case Token.RealLiteral:
				return new Ast.Expr.Literal(loc, LiteralValue.Real.of(double.Parse(tokenValue)));
			case Token.StringLiteral:
				return new Ast.Expr.Literal(loc, LiteralValue.String.of(tokenValue));
			case Token.Pass:
				return new Ast.Expr.Literal(loc, LiteralValue.Pass.instance);
			case Token.True:
			case Token.False:
				return new Ast.Expr.Literal(loc, LiteralValue.Bool.of(token == Token.True));
			case Token.Self:
				return new Ast.Expr.Self(loc);
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
		//Must take at least one non-'else' part.
		var firstCaseStartPos = pos;
		var firstTest = parseExprAndEndContext(Ctx.Plain, Next.Indent);
		var firstResult = parseBlock();
		var firstCase = new Ast.Expr.WhenTest.Case(locFrom(firstCaseStartPos), firstTest, firstResult);

		//TODO: support arbitrary number of clauses
		takeSpecificKeyword(Token.Else);
		takeIndent();
		var elseResult = parseBlock();

		return new Ast.Expr.WhenTest(locFrom(startPos), Arr.of(firstCase), elseResult);
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
		takeSpecificKeyword(Token.Do);
		takeIndent();
		var do_ = parseBlock();

		var catch_ = Op<Ast.Expr.Try.Catch>.None;
		var finally_ = Op<Ast.Expr>.None;

		var catchStart = pos;

		switch (takeKeyword()) {
			case Token.Catch: {
				takeSpace();
				var exceptionType = parseTy();
				takeSpace();
				var nameStart = pos;
				var exceptionName = takeName();
				var nameLoc = locFrom(nameStart);
				takeIndent();
				var catchBlock = parseBlock();
				catch_ = Op.Some(new Ast.Expr.Try.Catch(locFrom(catchStart), exceptionType, nameLoc, exceptionName, catchBlock));

				if (tryTakeDedent())
					break;
				else {
					takeSpecificKeyword(Token.Finally);
					goto case Token.Finally;
				}
			}

			case Token.Finally: {
				takeIndent();
				finally_ = Op.Some(parseBlock());
				takeDedent();
				break;
			}
		}

		return new Ast.Expr.Try(locFrom(startPos), do_, catch_, finally_);
	}

	static Ast.Pattern partsToPattern(Loc loc, Arr.Builder<Ast.Expr> parts) {
		switch (parts.curLength) {
			case 0: throw exit(loc, Err.PrecedingEquals);
			case 1: return partToPattern(parts[0]);
			default: return new Ast.Pattern.Destruct(loc, parts.map(partToPattern));
		}

		Ast.Pattern partToPattern(Ast.Expr part) {
			switch (part) {
				case Ast.Expr.Access a:
					return new Ast.Pattern.Single(a.loc, a.name);
				default:
					throw exit(loc, Err.PrecedingEquals);
			}
		}
	}
}
