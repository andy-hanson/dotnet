using Diag;

abstract class DiagnosticBuilder {
	readonly Arr.Builder<Diagnostic> errs = Arr.builder<Diagnostic>();

	internal void addDiagnostic(Loc loc, DiagnosticData diag) =>
		errs.add(new Diagnostic(loc, diag));

	internal Arr<Diagnostic> finishDiagnostics() =>
		errs.finish();
}
