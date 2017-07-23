using static StringUtils;

namespace Diag.ParseDiags {
	sealed class TooMuchIndent : Diag<TooMuchIndent> {
		internal readonly uint expected;
		internal readonly uint actual;
		internal TooMuchIndent(uint expected, uint actual) { this.expected = expected; this.actual = actual; }

		internal override string show() =>
			$"Too much indent. Expected at most {expected}, got {actual}.";

		public override bool deepEqual(TooMuchIndent e) => true;
		public override Dat toDat() => Dat.of(this,
			nameof(expected), Dat.nat(expected),
			nameof(actual), Dat.nat(actual));
	}

	sealed class LeadingSpace : NoDataDiag<LeadingSpace> { //TODO: kill, allow space indentation.
		internal static readonly LeadingSpace instance = new LeadingSpace();
		internal override string show() =>
			"Leading space!";
	}

	sealed class TrailingSpace : NoDataDiag<TrailingSpace> {
		internal static readonly TrailingSpace instance = new TrailingSpace();

		internal override string show() =>
			"Trailing space!";
	}

	sealed class EmptyExpression : NoDataDiag<EmptyExpression> {
		internal static readonly EmptyExpression instance = new EmptyExpression();

		internal override string show() =>
			"Empty expression";
	}

	sealed class BlockCantEndInLet : NoDataDiag<BlockCantEndInLet> {
		internal static readonly BlockCantEndInLet instance = new BlockCantEndInLet();

		internal override string show() =>
			"Block can't end in '='";
	}

	sealed class PrecedingEquals : NoDataDiag<PrecedingEquals> {
		internal static readonly PrecedingEquals instance = new PrecedingEquals();

		internal override string show() =>
			"Unusual expression preceding '='";
	}

	internal sealed class UnrecognizedCharacter : Diag<UnrecognizedCharacter> {
		internal readonly char ch;
		internal UnrecognizedCharacter(char ch) { this.ch = ch; }

		internal override string show() =>
			$"Unrecognized character {showChar(ch)}";

		public override bool deepEqual(UnrecognizedCharacter e) => ch == e.ch;
		public override Dat toDat() => Dat.of(this, nameof(ch), Dat.str(ch.ToString()));
	}

	internal sealed class UnexpectedCharacter : Diag<UnexpectedCharacter> {
		internal readonly char actual;
		internal readonly string expected;
		internal UnexpectedCharacter(char actual, string expected) { this.actual = actual; this.expected = expected; }

		internal override string show() =>
			$"Expected character to be {expected}, got {showChar(actual)}";

		public override bool deepEqual(UnexpectedCharacter e) => actual == e.actual && expected == e.expected;
		public override Dat toDat() => Dat.of(this, nameof(actual), Dat.str(actual.ToString()), nameof(expected), Dat.str(expected));
	}

	internal sealed class UnexpectedToken : Diag<UnexpectedToken> {
		internal readonly string expected;
		internal readonly string actual;
		internal UnexpectedToken(string expected, string actual) { this.expected = expected; this.actual = actual; }

		internal override string show() =>
			$"Unexpected token {actual}, expecting {expected}";

		public override bool deepEqual(UnexpectedToken e) => expected == e.expected && actual == e.actual;
		public override Dat toDat() => Dat.of(this, nameof(expected), Dat.str(expected), nameof(actual), Dat.str(actual));
	}
}
