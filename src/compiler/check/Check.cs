using Diag;
using Diag.CheckDiags;
using Model;
using static Utils;

sealed class Checker : CheckerCommon {
	internal static (ClassDeclaration, Arr<Diagnostic>) checkClass(Module module, Arr<Imported> imports, Ast.ClassDeclaration ast, Sym name) {
		var typeParameters = ast.typeParameters.map(TypeParameter.create);
		var klass = new ClassDeclaration(module, ast.loc, name, typeParameters);
		foreach (var t in typeParameters)
			t.origin = klass;
		var diags = Arr.builder<Diagnostic>();
		var ckr = new Checker(new BaseScope(klass, imports), diags);
		ckr.checkClass(klass, ast);
		return (klass, diags.finish());
	}

	Checker(BaseScope baseScope, Arr.Builder<Diagnostic> diags) : base(baseScope, diags) {}

	void checkClass(ClassDeclaration klass, Ast.ClassDeclaration ast) {
		var (loc, typeParameters, headAst, superAsts, methodAsts) = ast;

		var membersBuilder = Dict.builder<Sym, MemberDeclaration>();

		var methods = methodAsts.map(m => checkMethodInitial(m, klass, membersBuilder));
		klass.methods = methods;

		// Adds slot
		klass.head = checkHead(klass, headAst, membersBuilder);

		klass.setMembersMap(membersBuilder.finish());

		// Not that all members exist, we can fill in bodies.
		klass.setSupers(superAsts.mapDefinedProbablyAll(superAst => checkSuper(superAst, klass)));

		// Now that all methods exist, fill in the body of each member.
		methodAsts.doZip(methods, (methodAst, method) => {
			method.body = ExprChecker.checkMethod(baseScope, diags, method, TyReplacer.doNothingReplacer, method.isStatic, methodAst.body);
		});
	}

	MethodWithBody checkMethodInitial(Ast.Method m, ClassDeclaration klass, Dict.Builder<Sym, MemberDeclaration> membersBuilder) {
		// Not checking body yet -- fill in all method heads first.
		var (mloc, isStatic, typeParameterAsts, returnTyAst, name, selfEffect, parameterAsts, _) = m;
		var typeParameters = typeParameterAsts.map(TypeParameter.create);
		var returnTy = getTyOrTypeParameter(returnTyAst, typeParameters);
		var parameters = checkParameters(parameterAsts, typeParameters);
		var method = new MethodWithBody(klass, mloc, isStatic, typeParameters, returnTy, name, selfEffect, parameters);
		foreach (var tp in typeParameters)
			tp.origin = method;
		addMember(membersBuilder, method);
		return method;
	}

	ClassHead checkHead(ClassDeclaration klass, Op<Ast.ClassDeclaration.Head> ast, Dict.Builder<Sym, MemberDeclaration> membersBuilder) {
		if (!ast.get(out var h)) {
			if (klass.typeParameters.any)
				throw TODO(); // Error: static class can't have type parameters
			return ClassHead.Static.instance;
		}

		switch (h) {
			case Ast.ClassDeclaration.Head.Abstract a: {
				var abstractMethods = a.abstractMethods.map<AbstractMethodLike>(abstractMethodAst => {
					var (loc, typeParameterAsts, returnTyAst, name, selfEffect, parameterAsts) = abstractMethodAst;
					var typeParameters = typeParameterAsts.map(TypeParameter.create);
					var abs = new AbstractMethod(klass, loc, typeParameters, getTy(returnTyAst), name, selfEffect, checkParameters(parameterAsts, typeParameters));
					foreach (var tp in typeParameters)
						tp.origin = abs;
					addMember(membersBuilder, abs);
					return abs;
				});
				return new ClassHead.Abstract(a.loc, abstractMethods);
			}

			case Ast.ClassDeclaration.Head.Slots slotsAst: {
				var slots = new ClassHead.Slots(slotsAst.loc, klass);
				slots.slots = slotsAst.slots.map(slot => checkSlot(slot, slots, membersBuilder));
				return slots;
			}

			default:
				throw unreachable();
		}
	}

	SlotDeclaration checkSlot(Ast.Slot slotAst, ClassHead.Slots slots, Dict.Builder<Sym, MemberDeclaration> membersBuilder) {
		var (loc, mutable, tyAst, name) = slotAst;
		var slot = new SlotDeclaration(slots, loc, mutable, getTy(tyAst), name);
		addMember(membersBuilder, slot);
		return slot;
	}

	Op<Super> checkSuper(Ast.Super superAst, ClassDeclaration klass) {
		var (loc, name, tyArgs, implAsts) = superAst;
		if (!baseScope.accessClassDeclarationOrAddDiagnostic(loc, diags, name, out var superClass))
			return Op<Super>.None;

		var superInstCls = InstCls.of(superClass, tyArgs.map(getTy));
		var super = new Super(loc, klass, superInstCls);
		var (abstractMethods, abstractMethodTyReplacer) = getAbstractMethods(super.loc, superInstCls);
		super.impls = getImpls(abstractMethods, abstractMethodTyReplacer, implAsts, super);
		return Op.Some(super);
	}

	(Arr<AbstractMethodLike>, TyReplacer) getAbstractMethods(Loc loc, InstCls superClass) {
		var replacer = TyReplacer.ofInstCls(superClass);
		var (clsDecl, _) = superClass; // Just used tyArgs above

		if (clsDecl.supers.any)
			throw TODO(); //TODO: also handle abstract methods in superclass of superclass

		switch (clsDecl) {
			case BuiltinClass b:
				if (!b.isAbstract) {
					addDiagnostic(loc, new NotAnAbstractClass(b));
					return (Arr.empty<AbstractMethodLike>(), replacer);
				}
				return (b.abstractMethods, replacer);

			case ClassDeclaration k:
				if (!(k.head is ClassHead.Abstract abs)) {
					addDiagnostic(loc, new NotAnAbstractClass(k));
					return (Arr.empty<AbstractMethodLike>(), replacer);
				}
				return (abs.abstractMethods, replacer);

			default:
				throw unreachable();
		}
	}

	// `replacer` is for the case of e.g. `is Super[Nat]`.
	Arr<Impl> getImpls(Arr<AbstractMethodLike> abstractMethods, TyReplacer replacer, Arr<Ast.Impl> implAsts, Super super) {
		if (abstractMethods.length == 0)
			// We return an empty array on failure to find the superclass. So don't error again.
			return Arr.empty<Impl>();

		if (!abstractMethods.eachCorresponds(implAsts, (implemented, implAst) => implemented.name == implAst.name)) {
			addDiagnostic(super.loc, new ImplsMismatch(abstractMethods));
			// They didn't implement the right methods, so don't bother giving errors inside.
			return Arr.empty<Impl>();
		}

		return abstractMethods.zip(implAsts, (implemented, implAst) =>
			checkImpl(implAst, implemented, replacer, super));
	}

	Impl checkImpl(Ast.Impl implAst, AbstractMethodLike implemented, TyReplacer replacer, Super super) {
		var (loc, name, parameterNames, bodyAst) = implAst;

		// implAst.parameters is just for show -- we already have the real list of parameters.
		// So we just issue diagnostics if `impl.parameters` doesn't match `implemented.parameters`, otherwise we ignore it.
		if (!implemented.parameters.eachCorresponds(parameterNames, (implementedParam, implParam) => implParam.deepEqual(implementedParam.name)))
			addDiagnostic(loc, new WrongImplParameters(implemented));

		var impl = new Impl(super, loc, implemented);
		impl.body = ExprChecker.checkMethod(baseScope, diags, impl, replacer, /*isStatic*/ false, bodyAst);
		return impl;
	}

	void addMember(Dict.Builder<Sym, MemberDeclaration> membersBuilder, MemberDeclaration member) {
		if (!membersBuilder.tryAdd(member.name, member, out var oldMember))
			addDiagnostic(member.loc, new DuplicateMember(oldMember, member));
	}

	Arr<Parameter> checkParameters(Arr<Ast.Parameter> paramAsts, Arr<TypeParameter> typeParameters) =>
		paramAsts.map((parameter, index) => {
			var (loc, tyAst, name) = parameter;
			for (uint j = 0; j < index; j++)
				if (paramAsts[j].name == name)
					addDiagnostic(loc, new DuplicateParameterName(name));
			return new Parameter(loc, getTyOrTypeParameter(tyAst, typeParameters), name, index);
		});
}
