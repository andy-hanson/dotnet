using Model;
using static Utils;

struct BaseScope {
	internal readonly Klass self;
	readonly Arr<Module> imports;

	internal bool hasMember(Sym name) =>
		self.membersMap.has(name);

	internal bool tryGetMember(Sym name, out Member member) =>
		self.membersMap.get(name, out member);

	internal BaseScope(Klass self, Arr<Module> imports) {
		this.self = self; this.imports = imports;
		for (uint i = 0; i < imports.length; i++) {
			var import = imports[i];
			if (import.name == self.name)
				throw TODO(); // diagnostic -- can't shadow self
			for (uint j = 0; j < i; j++)
				if (imports[j].name == import.name)
					throw TODO(); // diagnostic -- can't shadow another import
		}
	}

	internal Ty getTy(Ast.Ty ast) {
		switch (ast) {
			case Ast.Ty.Access access:
				return accessTy(access.loc, access.name);
			case Ast.Ty.Inst inst:
				var a = accessTy(inst.instantiated.loc, inst.instantiated.name);
				var b = inst.tyArgs.map(getTy);
				unused(a, b);
				throw TODO(); //TODO: type instantiation
			default:
				throw unreachable();
		}
	}

	internal Ty accessTy(Loc loc, Sym name) {
		if (name == self.name)
			return self;

		if (imports.find(out var found, i => i.name == name))
			return found.klass;

		if (BuiltinClass.tryGet(name, out var builtin))
			return builtin;

		unused(loc);
		throw TODO(); //Return dummy Ty and issue diagnostic
	}
}
