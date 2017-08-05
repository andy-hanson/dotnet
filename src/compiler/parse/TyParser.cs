abstract class TyParser : Lexer {
	protected TyParser(string source) : base(source) {}

	protected Either<Model.Effect, Ast.Ty> parseSelfEffectOrTy() {
		var start = pos;
		var token = nextToken();
		switch (token) {
			case Token.Get:
			case Token.Set:
			case Token.Io: {
				takeSpace();
				var effect = token == Token.Get ? Model.Effect.get : token == Token.Set ? Model.Effect.set : Model.Effect.io;
				var name = nextToken();
				switch (name) {
					case Token.Self:
						return Either<Model.Effect, Ast.Ty>.Left(effect);
					case Token.TyName:
						return Either<Model.Effect, Ast.Ty>.Right(finishParseTy(start, effect, tokenSym));
					default:
						throw unexpected(start, "'self' or type name", name);
				}
			}
			case Token.TyName:
				return Either<Model.Effect, Ast.Ty>.Right(finishParseTy(start, Model.Effect.pure, tokenSym));
			default:
				throw unexpected(start, "'get', 'set', 'io', or type name", token);
		}
	}

	protected Ast.Ty parseTy() {
		var start = pos;
		var token = nextToken();
		switch (token) {
			case Token.Get:
			case Token.Set:
			case Token.Io:
				takeSpace();
				var effect = token == Token.Get ? Model.Effect.get : token == Token.Set ? Model.Effect.set : Model.Effect.io;
				return finishParseTy(start, effect, takeTyName());
			case Token.TyName:
				return finishParseTy(start, Model.Effect.pure, tokenSym);
			default:
				throw unexpected(start, "'get', 'set', 'io', or type name", token);
		}
	}

	protected Arr<Sym> tryTakeTypeParameters() =>
		!tryTakeLbracket() ? Arr.empty<Sym>() : Arr.build2(() => {
			var ty = takeTyName();
			var isNext = !tryTakeRbracket();
			if (isNext) {
				takeComma();
				takeSpace();
			}
			return (ty, isNext);
		});

	Ast.Ty finishParseTy(Pos start, Model.Effect effect, Sym name) {
		var args = tryTakeTypeArguments();
		return new Ast.Ty(locFrom(start), effect, name, args);
	}

	protected Arr<Ast.Ty> tryTakeTypeArguments() =>
		tryTakeLbracket() ? takeTypeArgumentsAfterPassingLbracket() : Arr.empty<Ast.Ty>();

	protected Arr<Ast.Ty> takeTypeArgumentsAfterPassingLbracket() =>
		Arr.build2(() => {
			var ty = parseTy();
			var isNext = !tryTakeRbracket();
			if (isNext) {
				takeComma();
				takeSpace();
			}
			return (ty, isNext);
		});
}
