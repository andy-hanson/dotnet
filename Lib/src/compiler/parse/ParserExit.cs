using System;

sealed class ParserExit : Exception {
	internal readonly CompileError err;
	ParserExit(Loc loc, Err err) { this.err = new CompileError(loc, err); }

	internal static ParserExit exit(Loc loc, Err err) => new ParserExit(loc, err);
}
