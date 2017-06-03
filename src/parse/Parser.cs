using System;
using System.Collections.Immutable;
using System.Diagnostics;
using static Utils;

sealed class Parser : Lexer {
    private Parser(string source) : base(source) {}

    Ast.Module parseModule(Sym name) {
        var start = pos;
        var kw = takeKeyword();

        ImmutableArray<Ast.Module.Import> imports;
        var classStart = start;
        var nextKw = kw;
        if (kw == Token.Import) {
            imports = buildUntilNull(parseImport);
            classStart = pos;
            nextKw = takeKeyword();
        } else {
            imports = ImmutableArray.Create<Ast.Module.Import>();
        }

        var klass = parseClass(name, classStart, nextKw);
        return new Ast.Module(locFrom(start), imports, klass);
    }

    private Op<Ast.Module.Import> parseImport() => TODO<Op<Ast.Module.Import>>("");

    private Ast.Klass parseClass(Sym name, int start, Token kw) {
        var head = parseHead(start, kw);
        var members = buildUntilNull(parseMember);
        return new Ast.Klass(locFrom(start), name, head, members);
    }

    private Ast.Klass.Head parseHead(int start, Token kw) => TODO<Ast.Klass.Head>();
    private Op<Ast.Member> parseMember() => TODO<Op<Ast.Member>>();

    private static ImmutableArray<T> buildUntilNull<T>(Func<Op<T>> f) {
        var b = ImmutableArray.CreateBuilder<T>();
        while (true) {
            var x = f();
            if (!x.hasValue)
                return b.ToImmutable();
            b.Add(x.get);
        }
    }
}

//mv
struct Op<T> {
    private readonly T value;
    internal Op(T value) { this.value = value; }
    internal bool hasValue => value != null;
    internal T get => _get();
    private T _get() {
        Debug.Assert(hasValue);
        return value;
    }
}



//!!!

/*

abstract class NameLike {
    public readonly Sym name;
    public NameLike(Sym name) { this.name = name; }
    public override string ToString() => $"{GetType().Name}({name})";
}
class Name : NameLike {
    public Name(Sym name) : base(name) {}
}
class TyName : NameLike {
    public TyName(Sym name) : base(name) {}
}
class Operator : NameLike {
    public Operator(Sym name) : base(name) {}
}
class Literal : Token {
    public readonly Model.LiteralValue value;
    Literal(Model.LiteralValue value) { this.value = value; }
    public override string ToString() => value.ToString();
}
class QuoteStart : Token {
    public readonly string head;
    QuoteStart(string head) { this.head = head; }
}
class Keyword : Token {
    public readonly Kw kw;
    Keyword(Kw kw) { this.kw = kw; }

    public override string ToString() => KwName(kw);
}*/
