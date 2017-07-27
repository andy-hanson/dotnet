using Diag;
using static Arr;
using static Utils;

sealed class Parser : ExprParser {
	internal static Either<Ast.Module, Diagnostic> parse(string source) {
		try {
			return Either<Ast.Module, Diagnostic>.Left(parseOrFail(source));
		} catch (ParserExitException e) {
			return Either<Ast.Module, Diagnostic>.Right(e.diagnostic);
		}
	}

	static Ast.Module parseOrFail(string source) => new Parser(source).parseModule();

	Parser(string source) : base(source) {}

	Ast.Module parseModule() {
		var start = pos;
		var kw = nextToken();

		Arr<Ast.Import> imports;
		var classStart = start;
		Token nextKw;
		if (kw == Token.Import) {
			imports = buildUntilNull(parseImport);
			classStart = pos;
			nextKw = nextToken();
		} else {
			imports = Arr.empty<Ast.Import>();
			nextKw = kw;
		}

		var klass = parseClass(classStart, nextKw);
		return new Ast.Module(locFrom(start), imports, klass);
	}

	//`import foo .bar ..baz`
	Op<Ast.Import> parseImport() {
		if (tryTakeNewline())
			return Op<Ast.Import>.None;

		takeSpace();

		var startPos = pos;
		uint leadingDots = 0;
		while (tryTakeDot()) leadingDots++;

		var pathParts = Arr.builder<string>();
		pathParts.add(takeTyNameString());
		//TODO: really want to allow delving into grandchildren?
		while (tryTakeDot()) pathParts.add(takeNameString());

		var path = new Path(pathParts.finish());
		var loc = locFrom(startPos);
		return Op.Some<Ast.Import>(leadingDots == 0
			? new Ast.Import.Global(loc, path).upcast<Ast.Import>()
			: new Ast.Import.Relative(loc, new RelPath(leadingDots, path)));
	}

	Ast.Klass parseClass(Pos start, Token kw) {
		var (head, nextStart, nextKw) = parseHead(start, kw);
		var (supers, nextNextStart, nextNextKw) = parseSupers(nextStart, nextKw);
		var methods = parseMethods(nextNextStart, nextNextKw);
		return new Ast.Klass(locFrom(start), head, supers, methods);
	}

	(Op<Ast.Klass.Head> head, Pos nextStart, MethodKw nextKeyword) parseHead(Pos start, Token kw) {
		switch (kw) {
			case Token.EOF:
			case Token.Fun:
				return (Op<Ast.Klass.Head>.None, start, kw == Token.EOF ? MethodKw.Eof : MethodKw.Fun);
			case Token.Abstract: {
				var head = parseAbstractHead(start);
				return (Op.Some<Ast.Klass.Head>(head), pos, takeMethodKeywordOrEof());
			}
			case Token.Slots: {
				var head = parseSlots(start);
				return (Op.Some<Ast.Klass.Head>(head), pos, takeMethodKeywordOrEof());
			}
			case Token.Enum:
				throw TODO();
			default:
				throw unexpected(start, "'abstract', 'static', 'slots' or 'enum'", kw);
		}
	}

	Ast.Klass.Head.Abstract parseAbstractHead(Pos start) {
		takeIndent();
		var abstractMethods = Arr.build2(() => {
			var methodStart = pos;
			var (returnTy, name, selfEffect, parameters) = parseMethodHead();
			var abs = new Ast.Klass.Head.Abstract.AbstractMethod(locFrom(methodStart), returnTy, name, selfEffect, parameters);
			var n = takeNewlineOrDedent();
			return (abs, n == NewlineOrDedent.Newline);
		});
		return new Ast.Klass.Head.Abstract(locFrom(start), abstractMethods);
	}

	(Arr<Ast.Super>, Pos, MethodKw) parseSupers(Pos start, MethodKw next) {
		var supers = Arr.builder<Ast.Super>();
		while (true) {
			if (next != MethodKw.Is)
				return (supers.finish(), start, next);

			supers.add(parseSuper(start));

			start = pos;
			next = takeMethodKeywordOrEof();
		}
	}

	Ast.Super parseSuper(Pos start) {
		takeSpace();
		var name = takeTyName();
		Arr<Ast.Impl> impls;
		switch (takeNewlineOrIndent()) {
			case NewlineOrIndent.Indent:
				impls = Arr.build2(parseImpl);
				break;
			case NewlineOrIndent.Newline:
				impls = Arr.empty<Ast.Impl>();
				break;
			default:
				throw unreachable();
		}

		return new Ast.Super(locFrom(start), name, impls);
	}

	(Ast.Impl, bool isNext) parseImpl() {
		// foo(x, y)
		var start = pos;
		var name = takeName();
		takeLparen();
		Arr<Sym> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Sym>();
		else {
			var first = takeName();
			parameters = buildUntilNullWithFirst(first, parseImplParameter);
		}

		takeIndent();
		var body = parseBlock();

		var impl = new Ast.Impl(locFrom(start), name, parameters, body);
		var isNext = !tryTakeDedentFromDedenting();
		return (impl, isNext);
	}

	Ast.Klass.Head.Slots parseSlots(Pos start) {
		takeIndent();
		var vars = build2(parseSlot); // At least one slot must exist.
		return new Ast.Klass.Head.Slots(locFrom(start), vars);
	}

	(Ast.Slot, bool isNext) parseSlot() {
		var start = pos;
		var next = takeSlotKeyword();
		bool mutable;
		switch (next) {
			case SlotKw.Val:
				mutable = false;
				break;
			case SlotKw.Var:
				mutable = true;
				break;
			default:
				throw unreachable();
		}

		takeSpace();
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		var slot = new Ast.Slot(locFrom(start), mutable, ty, name);
		var isNext = takeNewlineOrDedent() == NewlineOrDedent.Newline;
		return (slot, isNext);
	}

	Arr<Ast.Method> parseMethods(Pos start, MethodKw next) =>
		Arr.buildUntilNull(() => {
			var m = parseMethod(start, next);
			if (m.has) {
				start = pos;
				next = takeMethodKeywordOrEof();
			}
			return m;
		});

	Op<Ast.Method> parseMethod(Pos start, MethodKw next) {
		switch (next) {
			case MethodKw.Def:
			case MethodKw.Fun:
				var isStatic = next == MethodKw.Fun;
				takeSpace();
				var (returnTy, name, parameters, effect) = parseMethodHead();
				takeIndent();
				var body = parseBlock();
				return Op.Some(new Ast.Method(locFrom(start), isStatic, returnTy, name, parameters, effect, body));
			case MethodKw.Eof:
				return Op<Ast.Method>.None;
			case MethodKw.Is:
				throw unexpected(start, "'def' or 'fun'", Token.Is);
			default:
				throw unreachable();
		}
	}

	Op<Sym> parseImplParameter() {
		if (tryTakeRparen())
			return Op<Sym>.None;
		takeComma();
		takeSpace();
		return Op.Some(takeName());
	}

	(Ast.Ty, Sym, Model.Effect selfEffect, Arr<Ast.Parameter>) parseMethodHead() {
		var returnTy = parseTy();
		takeSpace();
		var name = takeName();
		takeLparen();

		var selfEffect = Model.Effect.Pure;
		Arr<Ast.Parameter> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Ast.Parameter>();
		else {
			var firstStart = pos;
			var x = parseSelfEffectOrTy();
			if (x.isLeft) {
				selfEffect = x.left;
				parameters = tryTakeRparen() ? Arr.empty<Ast.Parameter>() : buildUntilNull(parseParameter);
			} else {
				var firstTy = x.right;
				takeSpace();
				var firstName = takeName();
				var first = new Ast.Parameter(locFrom(firstStart), firstTy, firstName);
				parameters = buildUntilNullWithFirst(first, parseParameter);
			}
		}

		return (returnTy, name, selfEffect, parameters);
	}

	Op<Ast.Parameter> parseParameter() {
		if (tryTakeRparen())
			return Op<Ast.Parameter>.None;
		takeComma();
		takeSpace();
		return Op.Some(parseJustParameter());
	}

	Ast.Parameter parseJustParameter() {
		var start = pos;
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		return new Ast.Parameter(locFrom(start), ty, name);
	}
}
