using Diag;
using Diag.CheckDiags;
using Model;
using static Utils;

sealed class Checker : DiagnosticBuilder {
	internal static (Klass, Arr<Diagnostic>) checkClass(Module module, Arr<Module> imports, Ast.Klass ast, Sym name) {
		var klass = new Klass(module, ast.loc, name);
		var ckr = new Checker(new BaseScope(klass, imports));
		ckr.checkClass(klass, ast);
		var diagnostics = ckr.finishDiagnostics();
		return (klass, diagnostics);
	}

	readonly BaseScope baseScope;
	Checker(BaseScope baseScope) : base(Arr.builder<Diagnostic>()) { this.baseScope = baseScope; }

	void checkClass(Klass klass, Ast.Klass ast) {
		var membersBuilder = Dict.builder<Sym, Member>();

		var methods = ast.methods.map(m => {
			var e = new MethodWithBody(klass, m.loc, m.isStatic, baseScope.getTy(m.returnTy), m.name, m.selfEffect, checkParameters(m.parameters));
			addMember(membersBuilder, e);
			return e;
		});
		klass.methods = methods;

		// Adds slot
		klass.head = checkHead(klass, ast.head, membersBuilder);

		klass.setMembersMap(membersBuilder.finish());

		// Not that all members exist, we can fill in bodies.
		klass.setSupers(ast.supers.map(superAst => checkSuper(superAst, klass)));


		// Now that all methods exist, fill in the body of each member.
		ast.methods.doZip(methods, (methodAst, method) => {
			method.body = ExprChecker.checkMethod(baseScope, diags, method, method.isStatic, method, methodAst.body);
		});
	}

	KlassHead checkHead(Klass klass, Op<Ast.Klass.Head> ast, Dict.Builder<Sym, Member> membersBuilder) {
		if (!ast.get(out var h))
			return KlassHead.Static.instance;

		switch (h) {
			case Ast.Klass.Head.Abstract a: {
				var abstractMethods = a.abstractMethods.map<AbstractMethodLike>(am => {
					var abs = new AbstractMethod(klass, am.loc, baseScope.getTy(am.returnTy), am.name, am.selfEffect, checkParameters(am.parameters));
					addMember(membersBuilder, abs);
					return abs;
				});
				return new KlassHead.Abstract(a.loc, abstractMethods);
			}

			case Ast.Klass.Head.Slots slotsAst: {
				var slots = new KlassHead.Slots(slotsAst.loc, klass);
				slots.slots = slotsAst.slots.map(var => {
					var slot = new Slot(slots, slotsAst.loc, var.mutable, baseScope.getTy(var.ty), var.name);
					addMember(membersBuilder, slot);
					return slot;
				});
				return slots;
			}

			default:
				throw unreachable();
		}
	}

	Super checkSuper(Ast.Super superAst, Klass klass) {
		var superClass = (Klass)baseScope.accessClsRef(superAst.loc, superAst.name); //TODO: handle builtin
		var super = new Super(superAst.loc, klass, superClass);
		var abstractMethods = getAbstractMethods(super.loc, super.superClass);
		super.impls = getImpls(abstractMethods, superAst.impls, super);
		return super;
	}

	Arr<AbstractMethodLike> getAbstractMethods(Loc loc, ClsRef superClass) {
		if (!superClass.supers.isEmpty)
			//TODO: also handle abstract methods in superclass of superclass
			throw TODO();

		switch (superClass) {
			case BuiltinClass b:
				if (b.isAbstract)
					return b.abstractMethods;
				addDiagnostic(loc, new NotAnAbstractClass(b));
				return Arr.empty<AbstractMethodLike>();
			case Klass k: {
				if (k.head is KlassHead.Abstract abs)
					return abs.abstractMethods;
				addDiagnostic(loc, new NotAnAbstractClass(k));
				return Arr.empty<AbstractMethodLike>();
			}
			default:
				throw unreachable(); //TODO: generics
		}
	}

	Arr<Impl> getImpls(Arr<AbstractMethodLike> abstractMethods, Arr<Ast.Impl> implAsts, Super super) {
		if (abstractMethods.length == 0)
			// We return an empty array on failure to find the superclass. So don't error again.
			return Arr.empty<Impl>();

		if (!abstractMethods.eachCorresponds(implAsts, (implemented, implAst) => implemented.name == implAst.name)) {
			addDiagnostic(super.loc, new ImplsMismatch(abstractMethods));
			// They didn't implement the right methods, so don't bother giving errors inside.
			return Arr.empty<Impl>();
		}

		return abstractMethods.zip(implAsts, (implemented, implAst) => checkImpl(implAst, implemented, super));
	}

	Impl checkImpl(Ast.Impl implAst, AbstractMethodLike implemented, Super super) {
		// implAst.parameters is just for show -- we already have the real list of parameters.
		// So we just issue diagnostics if `impl.parameters` doesn't match `implemented.parameters`, otherwise we ignore it.
		if (!implemented.parameters.eachCorresponds(implAst.parameters, (implementedParam, implParam) => implParam.deepEqual(implementedParam.name)))
			addDiagnostic(implAst.loc, new WrongImplParameters(implemented));

		var impl = new Impl(super, implAst.loc, implemented);
		impl.body = ExprChecker.checkMethod(baseScope, diags, impl, /*isStatic*/ false, implemented, implAst.body);
		return impl;
	}

	void addMember(Dict.Builder<Sym, Member> membersBuilder, Member member) {
		if (!membersBuilder.tryAdd(member.name, member, out var oldMember))
			addDiagnostic(member.loc, new DuplicateMember(oldMember, member));
	}

	Arr<Parameter> checkParameters(Arr<Ast.Parameter> paramAsts) =>
		paramAsts.map((p, index) => {
			for (uint j = 0; j < index; j++)
				if (paramAsts[j].name == p.name)
					addDiagnostic(p.loc, new DuplicateParameterName(p.name));
			return new Parameter(p.loc, baseScope.getTy(p.ty), p.name, index);
		});
}
