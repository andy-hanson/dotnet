using Diag;

abstract class DiagnosticBuilder {
	protected readonly Arr.Builder<Diagnostic> diags;
	protected DiagnosticBuilder(Arr.Builder<Diagnostic> diags) { this.diags = diags; }

	internal void addDiagnostic(Loc loc, DiagnosticData diag) =>
		diags.add(new Diagnostic(loc, diag));

	internal Arr<Diagnostic> finishDiagnostics() =>
		diags.finish();
}
