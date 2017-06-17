using System;

using Model;
using static StringU;

struct CompileError : ToData<CompileError>, IEquatable<CompileError> {
	internal readonly Loc loc;
	internal readonly Err err;
	internal CompileError(Loc loc, Err err) { this.loc = loc; this.err = err; }

	public bool Equals(CompileError e) => loc.Equals(e.loc) && err.Equals(e.err);
	public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(err), err);
}

abstract class Err : ToData<Err>, IEquatable<Err> {
	Err() {}

	public abstract bool Equals(Err e);
	public abstract Dat toDat();

	internal abstract string Show { get; }

	internal abstract class ErrImpl<Self> : Err, ToData<Self> where Self : Err, ToData<Self> {
		public override bool Equals(Err e) => e is Self s && Equals(s);
		public abstract bool Equals(Self s);
	}

	internal static Err TooMuchIndent => new PlainErr("Too much indent!");
	internal static Err LeadingSpace => new PlainErr("Leading space!");
	internal static Err TrailingSpace => new PlainErr("Trailing space");
	internal static Err EmptyExpression => new PlainErr("Empty expression");
	internal static Err BlockCantEndInLet => new PlainErr("Block can't end in '='");
	internal static Err PrecedingEquals => new PlainErr("Unusual expression preceding '='");
	sealed class PlainErr : ErrImpl<PlainErr> {
		readonly string message;
		internal PlainErr(string message) { this.message = message; }
		internal override string Show => message;

		public override bool Equals(PlainErr e) => message == e.message;
		public override Dat toDat() => Dat.str(message);
	}

	internal sealed class UnrecognizedCharacter : ErrImpl<UnrecognizedCharacter> {
		internal readonly char ch;
		internal UnrecognizedCharacter(char ch) { this.ch = ch; }
		internal override string Show => $"Unrecognized character {showChar(ch)}";

		public override bool Equals(UnrecognizedCharacter e) => ch == e.ch;
		public override Dat toDat() => Dat.of(this, nameof(ch), Dat.str(ch.ToString()));
	}

	internal sealed class UnexpectedCharacter : ErrImpl<UnexpectedCharacter> {
		internal readonly char actual;
		internal readonly string expected;
		internal UnexpectedCharacter(char actual, string expected) { this.actual = actual; this.expected = expected; }
		internal override string Show => $"Expected character to be {expected}, got {showChar(actual)}";

		public override bool Equals(UnexpectedCharacter e) => actual == e.actual && expected == e.expected;
		public override Dat toDat() => Dat.of(this, nameof(actual), Dat.str(actual.ToString()), nameof(expected), Dat.str(expected));
	}

	internal sealed class CircularDependency : ErrImpl<CircularDependency> {
		internal readonly Path path;
		internal CircularDependency(Path path) { this.path = path; }
		internal override string Show => $"Circular dependency at module {path}";

		public override bool Equals(CircularDependency c) => path.Equals(c.path);
		public override Dat toDat() => Dat.of(this, nameof(path), path);
	}

	internal sealed class CantFindLocalModule : ErrImpl<CantFindLocalModule> {
		internal readonly Path logicalPath;
		internal CantFindLocalModule(Path logicalPath) { this.logicalPath = logicalPath; }

		internal override string Show =>
			$"Can't find module '{logicalPath}'.\nTried '{ModuleResolver.attemptedPaths(logicalPath)}'.";

		public override bool Equals(CantFindLocalModule c) => logicalPath.Equals(c.logicalPath);
		public override Dat toDat() => Dat.of(this, nameof(logicalPath), logicalPath);
	}

	internal sealed class UnexpectedToken : ErrImpl<UnexpectedToken> {
		internal readonly string expected;
		internal readonly string actual;
		internal UnexpectedToken(string expected, string actual) { this.expected = expected; this.actual = actual; }

		internal override string Show => $"Unexpected token {actual}, expecting {expected}";

		public override bool Equals(UnexpectedToken e) => expected == e.expected && actual == e.actual;
		public override Dat toDat() => Dat.of(this, nameof(expected), Dat.str(expected), nameof(actual), Dat.str(actual));
	}

	internal sealed class CombineTypes : ErrImpl<CombineTypes> {
		internal readonly Ty ty1;
		internal readonly Ty ty2;
		internal CombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }
		internal override string Show => $"Mismatch in type inference: {ty1}, {ty2}";

		public override bool Equals(CombineTypes e) => ty1.Equals(e.ty1) && ty2.Equals(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1, nameof(ty2), ty2);
	}
}
