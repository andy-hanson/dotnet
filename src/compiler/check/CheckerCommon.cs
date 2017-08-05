using Diag;
using Model;
using static Utils;

abstract class CheckerCommon {
	protected readonly BaseScope baseScope;
	protected readonly Arr.Builder<Diagnostic> diags;
	protected CheckerCommon(BaseScope baseScope, Arr.Builder<Diagnostic> diags) { this.baseScope = baseScope; this.diags = diags; }

	protected Ty getTy(Ast.Ty ast) =>
		baseScope.getTy(ast, diags);

	protected Ty getTyOrTypeParameter(Ast.Ty ast, Arr<TypeParameter> typeParameters) {
		var (loc, effect, name, tyArgs) = ast;
		foreach (var tp in typeParameters) {
			if (name == tp.name) {
				if (!effect.deepEqual(Effect.pure))
					throw TODO(); // Diagnostic: Not allowed to specify an effect on a type parameter.
				if (tyArgs.any)
					throw TODO(); // Diagnostic: Higher-kinded types not supported
				return tp;
			}
		}
		return getTy(ast);
	}

	protected void addDiagnostic(Loc loc, Diag.DiagnosticData diag) =>
		diags.add(new Diagnostic(loc, diag));
}
