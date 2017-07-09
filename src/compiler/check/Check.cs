using Model;
using static Utils;

class Checker {
	internal static Klass checkClass(Module module, Arr<Module> imports, Ast.Klass ast, Sym name) {
		var klass = new Klass(module, ast.loc, name);
		return new Checker(klass, imports).checkClass(klass, ast);
	}

	readonly BaseScope baseScope;

	Checker(Klass klass, Arr<Module> imports) {
		this.baseScope = new BaseScope(klass, imports);
	}

	static Arr<Method> getAbstractMethods(Ty superClass) {
		if (!superClass.supers.isEmpty)
			//TODO: also handle abstract methods in superclass of superclass
			throw TODO();

		switch (superClass) {
			case BuiltinClass b:
				return b.abstractMethods;
			case Klass k: {
				if (!(k.head is Klass.Head.Abstract abs))
					throw TODO();
				return abs.abstractMethods;
			}
			default:
				throw TODO();
		}
	}

	static void addMember(Dict.Builder<Sym, Member> membersBuilder, Member member) {
		if (!membersBuilder.tryAdd(member.name, member))
			throw TODO(); //diagnostic
	}

	Klass checkClass(Klass klass, Ast.Klass ast) {
		var membersBuilder = Dict.builder<Sym, Member>();

		var methods = ast.methods.map(methodAst => {
			var e = emptyMethod(klass, methodAst);
			addMember(membersBuilder, e);
			return e;
		});
		klass.methods = methods;

		// Adds slot
		klass.head = checkHead(klass, ast.head, membersBuilder, methods);

		klass.setMembersMap(membersBuilder.finish());

		// Not that all members exist, we can fill in bodies.
		klass.setSupers(ast.supers.map(superAst => {
			var superClass = (Klass)baseScope.accessTy(superAst.loc, superAst.name); //TODO: handle builtin
			var super = new Super(superAst.loc, klass, superClass);

			var abstractMethods = getAbstractMethods(super.superClass);

			var implAsts = superAst.impls;

			if (implAsts.length != abstractMethods.length)
				throw TODO(); // Something wasn't implemented.

			for (uint i = 0; i < implAsts.length; i++) {
				//Can't implement the same method twice (TEST)
				var name = implAsts[i].name;
				for (uint j = 0; j < i; j++) {
					if (implAsts[j].name == name)
						throw TODO(); // duplicate implementation
				}
			}

			super.impls = implAsts.map(implAst => {
				var name = implAst.name;
				if (!abstractMethods.find(out var implemented, a => a.name == name))
					throw TODO(); // Implemented a non-existent method.

				if (implAst.parameters.length != implemented.parameters.length)
					throw TODO(); // Too many / not enough parameters
				implAst.parameters.doZip(implemented.parameters, (implParameter, abstractParameter) => {
					if (implParameter != abstractParameter.name)
						throw TODO(); // Parameter names don't match
				});

				var impl = new Impl(super, implAst.loc, implemented);
				impl.body = ExprChecker.checkMethod(baseScope, impl, /*isStatic*/ false, implemented.returnTy, implemented.parameters, implemented.effect, implAst.body);
				return impl;
			});

			return super;
		}));

		// Now that all members exist, fill in the body of each member.
		ast.methods.doZip(methods, (memberAst, member) => {
			switch (memberAst) {
				case Ast.Member.Method methodAst:
					var method = (Method.MethodWithBody)member;
					method.body = ExprChecker.checkMethod(baseScope, method, method.isStatic, method.returnTy, method.parameters, method.effect, methodAst.body);
					break;
				case Ast.Member.AbstractMethod _:
					break;
				default:
					throw unreachable();
			}
		});

		return klass;
	}

	Klass.Head checkHead(Klass klass, Ast.Klass.Head ast, Dict.Builder<Sym, Member> membersBuilder, Arr<Method> methods) {
		var loc = ast.loc;
		switch (ast) {
			case Ast.Klass.Head.Static _:
				return new Klass.Head.Static(loc);

			case Ast.Klass.Head.Abstract _: {
				var abstractMethods = methods.keep(m => m is Method.AbstractMethod);
				return new Klass.Head.Abstract(loc, abstractMethods);
			}

			case Ast.Klass.Head.Slots slotsAst: {
				var slots = new Klass.Head.Slots(loc, klass);
				slots.slots = slotsAst.slots.map(var => {
					var slot = new Slot(slots, ast.loc, var.mutable, baseScope.getTy(var.ty), var.name);
					addMember(membersBuilder, slot);
					return slot;
				});
				return slots;
			}

			default:
				throw unreachable();
		}
	}

	Method emptyMethod(Klass klass, Ast.Member ast) {
		switch (ast) {
			case Ast.Member.Method m:
				return new Method.MethodWithBody(
					klass, m.loc, m.isStatic, baseScope.getTy(m.returnTy), m.name, getParams(m.parameters), ast.effect);
			case Ast.Member.AbstractMethod a:
				return new Method.AbstractMethod(
					klass, a.loc, baseScope.getTy(a.returnTy), a.name, getParams(a.parameters), ast.effect);
			default:
				throw unreachable();
		}
	}

	Arr<Parameter> getParams(Arr<Ast.Parameter> pms) =>
		pms.map((p, index) => new Parameter(p.loc, baseScope.getTy(p.ty), p.name, index));
}
