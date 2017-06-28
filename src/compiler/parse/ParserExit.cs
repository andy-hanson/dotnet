using System;

sealed class ParserExitException : Exception {
	internal readonly CompileError err;
	ParserExitException(Loc loc, Err err) { this.err = new CompileError(loc, err); }

	internal static ParserExitException exit(Loc loc, Err err) => new ParserExitException(loc, err);
}
