using Diag;
using Diag.CheckDiags;
using Model;
using static Utils;

sealed class Checker : CheckerCommon {
	internal static (Klass, Arr<Diagnostic>) checkClass(Module module, Arr<Imported> imports, Ast.Klass ast, Sym name) {
		var klass = new Klass(module, ast.loc, name);
		var diags = Arr.builder<Diagnostic>();
		var ckr = new Checker(new BaseScope(klass, imports), diags);
		ckr.checkClass(klass, ast);
		return (klass, diags.finish());
	}

	Checker(BaseScope baseScope, Arr.Builder<Diagnostic> diags) : base(baseScope, diags) {}

	void checkClass(Klass klass, Ast.Klass ast) {
		var (loc, headAst, superAsts, methodAsts) = ast;

		var membersBuilder = Dict.builder<Sym, Member>();

		var methods = methodAsts.map(m => checkMethodInitial(m, klass, membersBuilder));
		klass.methods = methods;

		// Adds slot
		klass.head = checkHead(klass, headAst, membersBuilder);

		klass.setMembersMap(membersBuilder.finish());

		// Not that all members exist, we can fill in bodies.
		klass.setSupers(superAsts.mapDefinedProbablyAll(superAst => checkSuper(superAst, klass)));

		// Now that all methods exist, fill in the body of each member.
		methodAsts.doZip(methods, (methodAst, method) => {
			method.body = ExprChecker.checkMethod(baseScope, diags, method, method.isStatic, method, methodAst.body);
		});
	}

	MethodWithBody checkMethodInitial(Ast.Method m, Klass klass, Dict.Builder<Sym, Member> membersBuilder) {
		// Not checking body yet -- fill in all method heads first.
		var (mloc, isStatic, returnTyAst, name, selfEffect, parameterAsts, _) = m;
		var e = new MethodWithBody(klass, mloc, isStatic, getTy(returnTyAst), name, selfEffect, checkParameters(parameterAsts));
		addMember(membersBuilder, e);
		return e;
	}

	KlassHead checkHead(Klass klass, Op<Ast.Klass.Head> ast, Dict.Builder<Sym, Member> membersBuilder) {
		if (!ast.get(out var h))
			return KlassHead.Static.instance;

		switch (h) {
			case Ast.Klass.Head.Abstract a: {
				var abstractMethods = a.abstractMethods.map<AbstractMethodLike>(am => {
					var abs = new AbstractMethod(klass, am.loc, getTy(am.returnTy), am.name, am.selfEffect, checkParameters(am.parameters));
					addMember(membersBuilder, abs);
					return abs;
				});
				return new KlassHead.Abstract(a.loc, abstractMethods);
			}

			case Ast.Klass.Head.Slots slotsAst: {
				var slots = new KlassHead.Slots(slotsAst.loc, klass);
				slots.slots = slotsAst.slots.map(slot => checkSlot(slot, slots, membersBuilder));
				return slots;
			}

			default:
				throw unreachable();
		}
	}

	Slot checkSlot(Ast.Slot slotAst, KlassHead.Slots slots, Dict.Builder<Sym, Member> membersBuilder) {
		var (loc, mutable, tyAst, name) = slotAst;
		var slot = new Slot(slots, loc, mutable, getTy(tyAst), name);
		addMember(membersBuilder, slot);
		return slot;
	}

	Op<Super> checkSuper(Ast.Super superAst, Klass klass) {
		var (loc, name, implAsts) = superAst;
		if (!baseScope.accessClsRefOrAddDiagnostic(loc, name, diags, out var superClass))
			return Op<Super>.None;

		var super = new Super(loc, klass, superClass);
		var abstractMethods = getAbstractMethods(super.loc, super.superClass);
		super.impls = getImpls(abstractMethods, implAsts, super);
		return Op.Some(super);
	}

	Arr<AbstractMethodLike> getAbstractMethods(Loc loc, ClsRef superClass) {
		if (superClass.supers.any)
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
		var (loc, name, parameterNames, bodyAst) = implAst;

		// implAst.parameters is just for show -- we already have the real list of parameters.
		// So we just issue diagnostics if `impl.parameters` doesn't match `implemented.parameters`, otherwise we ignore it.
		if (!implemented.parameters.eachCorresponds(parameterNames, (implementedParam, implParam) => implParam.deepEqual(implementedParam.name)))
			addDiagnostic(loc, new WrongImplParameters(implemented));

		var impl = new Impl(super, loc, implemented);
		impl.body = ExprChecker.checkMethod(baseScope, diags, impl, /*isStatic*/ false, implemented, bodyAst);
		return impl;
	}

	void addMember(Dict.Builder<Sym, Member> membersBuilder, Member member) {
		if (!membersBuilder.tryAdd(member.name, member, out var oldMember))
			addDiagnostic(member.loc, new DuplicateMember(oldMember, member));
	}

	Arr<Parameter> checkParameters(Arr<Ast.Parameter> paramAsts) =>
		paramAsts.map((parameter, index) => {
			var (loc, tyAst, name) = parameter;
			for (uint j = 0; j < index; j++)
				if (paramAsts[j].name == name)
					addDiagnostic(loc, new DuplicateParameterName(name));
			return new Parameter(loc, getTy(tyAst), name, index);
		});
}
