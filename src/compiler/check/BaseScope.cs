using Diag;
using Diag.CheckDiags;
using Diag.CheckExprDiags;
using Model;
using static Utils;

struct BaseScope {
	internal readonly ClassDeclaration currentClass;
	readonly Arr<Imported> imports;

	internal bool getOwnMemberOrAddDiagnostic(Loc loc, Sym name, Arr.Builder<Diagnostic> diags, out InstMember member) {
		if (!ClassUtils.tryGetMemberFromClassDeclaration(currentClass, name, out member)) {
			diags.add(new Diagnostic(loc, new MemberNotFound(currentClass, name)));
			return false;
		}
		return true;
	}

	internal BaseScope(ClassDeclaration currentClass, Arr<Imported> imports) {
		this.currentClass = currentClass;
		this.imports = imports;
		for (uint i = 0; i < imports.length; i++) {
			var import = imports[i];
			if (import.name == currentClass.name)
				throw TODO(); // diagnostic -- can't shadow self
			for (uint j = 0; j < i; j++)
				if (imports[j].name == import.name)
					throw TODO(); // diagnostic -- can't shadow another import
		}
	}

	internal Ty getTy(Ast.Ty ast, Arr.Builder<Diagnostic> diags) {
		var (loc, effect, name, tyArgs) = ast;
		if (!accessClassDeclarationOrAddDiagnostic(loc, diags, name, out var cls))
			return Ty.bogus;

		var self = this;
		return Ty.of(effect, InstCls.of(cls, tyArgs.map(tyAst => self.getTy(tyAst, diags))));
	}

	internal bool accessClassDeclarationOrAddDiagnostic(Loc loc, Arr.Builder<Diagnostic> diags, Sym name, out ClassDeclarationLike cls) {
		if (!accessClassDeclaration(name, out cls)) {
			diags.add(new Diagnostic(loc, new ClassNotFound(name)));
			return false;
		}
		return true;
	}

	bool accessClassDeclaration(Sym name, out ClassDeclarationLike cls) {
		if (name == currentClass.name) {
			cls = currentClass;
			return true;
		}

		if (imports.find(out var import, i => i.name == name)) {
			cls = import.importedClass;
			return true;
		}

		if (BuiltinsLoader.tryGetNoImportBuiltin(name, out var builtin)) {
			// out parameters are invariant, so need this line
			// https://stackoverflow.com/questions/527758/in-c-sharp-4-0-why-cant-an-out-parameter-in-a-method-be-covariant
			cls = builtin;
			return true;
		}

		cls = default(ClassDeclarationLike);
		return false;
	}
}
