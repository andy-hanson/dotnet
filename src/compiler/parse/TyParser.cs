abstract class TyParser : Lexer {
	protected TyParser(string source) : base(source) {}

	protected Either<Model.Effect, Ast.Ty> parseTyOrSelfEffect() {
		var start = pos;
		var token = nextToken();
		switch (token) {
			case Token.Get:
			case Token.Set:
			case Token.Io: {
				takeSpace();
				var effect = token == Token.Get ? Model.Effect.Get : token == Token.Set ? Model.Effect.Set : Model.Effect.Io;
				var nameStart = pos;
				var name = nextToken();
				switch (name) {
					case Token.Self:
						return Either<Model.Effect, Ast.Ty>.Left(effect);
					case Token.TyName:
						var ty = new Ast.Ty(locFrom(start), effect, new Ast.ClsRef.Access(locFrom(nameStart), tokenSym));
						return Either<Model.Effect, Ast.Ty>.Right(ty);
					default:
						throw unexpected(start, "'self' or type name", name);
				}
			}
			case Token.TyName: {
				var loc = locFrom(start);
				return Either<Model.Effect, Ast.Ty>.Right(new Ast.Ty(loc, Model.Effect.Pure, new Ast.ClsRef.Access(loc, tokenSym)));
			}
			default:
				throw unexpected(start, "'get', 'set', 'io', or type name", token);
		}
	}

	protected Ast.Ty parseTy() {
		var start = pos;
		var e = parseTyOrSelfEffect();
		if (e.isLeft)
			throw unexpected(start, "'self' parameter must be the first", Token.Self);
		return e.right;
	}
}
