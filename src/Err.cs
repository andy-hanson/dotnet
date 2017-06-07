using Model;

struct CompileError {//rename
    internal readonly Loc loc;
    internal readonly Err err;
    internal CompileError(Loc loc, Err err) { this.loc = loc; this.err = err; }
}

abstract class Err {
    Err() {}
    internal abstract string Show { get; }

    sealed class PlainErr : Err {
        readonly string message;
        internal PlainErr(string message) { this.message = message; }
        internal override string Show => message;
    }

    internal static Err TooMuchIndent => new PlainErr("Too much indent!");
    internal static Err LeadingSpace => new PlainErr("Leading space!");
    internal static Err TrailingSpace => new PlainErr("Trailing space");
    internal static Err EmptyExpression => new PlainErr("Empty expression");
    internal static Err BlockCantEndInLet => new PlainErr("Block can't end in '='");
    internal static Err PrecedingEquals => new PlainErr("Unusual expression preceding '='");

    internal sealed class UnrecognizedCharacter : Err {
        internal readonly char ch;
        internal UnrecognizedCharacter(char ch) { this.ch = ch; }
        internal override string Show => $"Unrecognized character {showChar(ch)}";
    }

    internal sealed class UnexpectedCharacter : Err {
        internal readonly char actual;
        internal readonly string expected;
        internal UnexpectedCharacter(char actual, string expected) { this.actual = actual; this.expected = expected; }
        internal override string Show => $"Expected character to be {expected}, got {showChar(actual)}";
    }

    internal sealed class CircularDependency : Err {
        internal readonly Path path;
        internal CircularDependency(Path path) { this.path = path; }
        internal override string Show => $"Circular dependency at module {path}";
    }

    internal sealed class CantFindLocalModule : Err {
        internal readonly Path logicalPath;
        internal CantFindLocalModule(Path logicalPath) { this.logicalPath = logicalPath; }

        internal override string Show =>
            $"Can't find module '{logicalPath}'.\nTried '{Compiler.attemptedPaths(logicalPath)}'.";
    }

    static string showChar(char ch) {
        switch (ch) {
            case '\n': return "'\\n'";
            case '\t': return "'\\t'";
            default: return $"'{ch}'";
        }
    }

    internal sealed class UnexpectedToken : Err {
        internal readonly string desc;
        internal UnexpectedToken(string desc) { this.desc = desc; }
        internal override string Show => $"Unexpected token {desc}";
    }

    internal sealed class CombineTypes : Err {
        internal readonly Ty ty1;
        internal readonly Ty ty2;
        internal CombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }
        internal override string Show => $"Mismatch in type inference: {ty1}, {ty2}";
    }
}
