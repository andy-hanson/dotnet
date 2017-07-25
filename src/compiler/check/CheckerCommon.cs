using Diag;

abstract class CheckerCommon {
	protected readonly BaseScope baseScope;
	protected readonly Arr.Builder<Diagnostic> diags;
	protected CheckerCommon(BaseScope baseScope, Arr.Builder<Diagnostic> diags) { this.baseScope = baseScope; this.diags = diags; }

	protected Model.Ty getTy(Ast.Ty ast) =>
		baseScope.getTy(ast, diags);

	protected void addDiagnostic(Loc loc, Diag.DiagnosticData diag) =>
		diags.add(new Diagnostic(loc, diag));
}
