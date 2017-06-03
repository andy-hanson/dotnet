using System;

abstract class Err {
    private Err() {}

    sealed class PlainErr : Err {
        readonly string message;
        internal PlainErr(string s) { this.message = s; }
    }

    internal static Err TooMuchIndent => new PlainErr("Too much indent!");
    internal static Err LeadingSpace => new PlainErr("Leading space!");
    internal static Err TrailingSpace => new PlainErr("Trailing space");

    internal sealed class UnrecognizedCharacter : Err {
        readonly char ch;
        internal UnrecognizedCharacter(char ch) { this.ch = ch; }
    }

    internal sealed class UnexpectedCharacter : Err {
        readonly char actual;
        readonly string expected;
        internal UnexpectedCharacter(char actual, string expected) { this.actual = actual; this.expected = expected; }
    }

    internal sealed class UnexpectedToken : Err {
        readonly string desc;
        internal UnexpectedToken(string desc) { this.desc = desc; }
    }
}


sealed class CompileError : Exception {
    readonly Loc loc;
    readonly Err err;
    internal CompileError(Loc loc, Err err) : base() {
        this.loc = loc;
        this.err = err;
    }
}

static class ErrU {
    internal static void raise(Loc loc, Err err) => throw new CompileError(loc, err);
    internal static T raise<T>(Loc loc, Err err) => throw new CompileError(loc, err);

    internal static void must(bool cond, Loc loc, Err err) {
        if (!cond) raise(loc, err);
    }
}
