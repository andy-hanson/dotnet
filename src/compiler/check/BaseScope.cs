using Diag;
using Diag.CheckDiags;
using Diag.CheckExprDiags;
using Model;
using static Utils;

struct BaseScope {
	internal readonly Klass self;
	readonly Arr<Module> imports;

	internal bool hasMember(Sym name) =>
		self.membersMap.has(name);

	internal bool tryGetOwnMember(Loc loc, Sym name, Arr.Builder<Diagnostic> diags, out Member member) {
		if (!self.membersMap.get(name, out member)) {
			diags.add(new Diagnostic(loc, new MemberNotFound(self, name)));
			return false;
		}
		return true;
	}

	internal BaseScope(Klass self, Arr<Module> imports) {
		this.self = self;
		this.imports = imports;
		for (uint i = 0; i < imports.length; i++) {
			var import = imports[i];
			if (import.name == self.name)
				throw TODO(); // diagnostic -- can't shadow self
			for (uint j = 0; j < i; j++)
				if (imports[j].name == import.name)
					throw TODO(); // diagnostic -- can't shadow another import
		}
	}

	internal Ty getTy(Ast.Ty ast, Arr.Builder<Diagnostic> diags) =>
		getClsRef(ast.cls, diags, out var cls) ? Ty.of(ast.effect, cls) : Ty.bogus;

	bool getClsRef(Ast.ClsRef ast, Arr.Builder<Diagnostic> diags, out ClsRef cls) {
		switch (ast) {
			case Ast.ClsRef.Access access:
				return accessClsRefOrAddDiagnostic(access.loc, access.name, diags, out cls);

			case Ast.ClsRef.Inst inst: {
				var self = this;
				var tyArgs = inst.tyArgs.map(x => self.getTy(x, diags));

				if (!accessClsRefOrAddDiagnostic(inst.instantiated.loc, inst.instantiated.name, diags, out var gen)) {
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
		if (name == self.name) {
			cls = self;
			return true;
		}

		if (imports.findMap(out cls, i => i.name == name ? Op.Some<ClsRef>(i.klass) : Op<ClsRef>.None))
			return true;

		if (BuiltinClass.tryGet(name, out var builtin)) {
			// out parameters are invariant, so need this line
			// https://stackoverflow.com/questions/527758/in-c-sharp-4-0-why-cant-an-out-parameter-in-a-method-be-covariant
			cls = builtin;
			return true;
		}

		cls = default(ClsRef);
		return false;
	}
}
