using Diag;

class DiagnosticBuilder {
	readonly Arr.Builder<Diagnostic> errs = Arr.builder<Diagnostic>();

	void add(Loc loc, DiagnosticData diag) =>
		errs.add(new Diagnostic(loc, diag));

	internal Arr<Diagnostic> finish() =>
		errs.finish();
}
