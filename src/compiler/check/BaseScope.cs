using Diag;
using Diag.CheckDiags;
using Diag.CheckExprDiags;
using Model;
using static Utils;

struct BaseScope {
	internal readonly Klass currentClass;
	readonly Arr<Imported> imports;

	internal bool tryGetOwnMember(Loc loc, Sym name, Arr.Builder<Diagnostic> diags, out Member member) {
		if (!currentClass.membersMap.get(name, out member)) {
			diags.add(new Diagnostic(loc, new MemberNotFound(currentClass, name)));
			return false;
		}
		return true;
	}

	internal BaseScope(Klass currentClass, Arr<Imported> imports) {
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
		var (_, effect, clsAst) = ast;
		return getClsRef(clsAst, diags, out var cls) ? Ty.of(effect, cls) : Ty.bogus;
	}

	bool getClsRef(Ast.ClsRef ast, Arr.Builder<Diagnostic> diags, out ClsRef cls) {
		switch (ast) {
			case Ast.ClsRef.Access access: {
				var (loc, name) = access;
				return accessClsRefOrAddDiagnostic(loc, name, diags, out cls);
			}

			case Ast.ClsRef.Inst inst: {
				var (loc, (instantiatedLoc, instantiatedName), tyArgAsts) = inst;

				var self = this;
				var tyArgs = tyArgAsts.map(x => self.getTy(x, diags));

				if (!accessClsRefOrAddDiagnostic(instantiatedLoc, instantiatedName, diags, out var gen)) {
					cls = default(ClsRef);
					return false;
				}

				unused(gen, tyArgs);
				throw TODO(); //TODO: type instantiation
			}

			default:
				throw unreachable();
		}
	}

	internal bool accessClsRefOrAddDiagnostic(Loc loc, Sym name, Arr.Builder<Diagnostic> diags, out ClsRef cls) {
		if (!accessClsRef(name, out cls)) {
			diags.add(new Diagnostic(loc, new ClassNotFound(name)));
			return false;
		}
		return true;
	}

	internal bool accessClsRef(Sym name, out ClsRef cls) {
		if (name == currentClass.name) {
			cls = currentClass;
			return true;
		}

		if (imports.findMap(out cls, i => i.name == name ? Op.Some<ClsRef>(i.importedClass) : Op<ClsRef>.None))
			return true;

		if (BuiltinClass.tryGetNoImportBuiltin(name, out var builtin)) {
			// out parameters are invariant, so need this line
			// https://stackoverflow.com/questions/527758/in-c-sharp-4-0-why-cant-an-out-parameter-in-a-method-be-covariant
			cls = builtin;
			return true;
		}

		cls = default(ClsRef);
		return false;
	}
}
