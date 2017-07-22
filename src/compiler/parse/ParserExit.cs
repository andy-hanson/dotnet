using System;

using Diag;

sealed class ParserExitException : Exception {
	internal readonly Diagnostic diagnostic;
	ParserExitException(Diagnostic diagnostic) { this.diagnostic = diagnostic; }

	internal static ParserExitException exit(Loc loc, DiagnosticData diag) => new ParserExitException(new Diagnostic(loc, diag));
}
