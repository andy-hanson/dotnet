using Model;

using static EstreeUtils;
using static NameEscaping;
using static Utils;

sealed class JsEmitter {
	internal static string emitToString(Module m) {
		var p = new JsEmitter().emitToProgram(m);
		return JsWriter.writeToString(p);
	}

	private bool needsLib = false;

	static Estree.Identifier baseId(string name) => id(Loc.zero, name);
	static readonly Estree.Identifier requireId = baseId("require");
	static readonly Estree.Statement requireNzlib = importStatement("_", "nzlib");

	static readonly Estree.Statement useStrict =
		Estree.ExpressionStatement.of(Estree.Literal.str(Loc.zero, "use strict"));

	Estree.Program emitToProgram(Module module) {
		var body = Arr.builder<Estree.Statement>();

		foreach (var import in module.imports)
			switch (import) {
				case Module m:
					body.add(emitImport(module, m));
					break;
				case BuiltinClass b:
					// Don't import builtin classes the normal way. They will be accessed through nzlib.
					break;
				default:
					throw unreachable();
			}

		var cls = emitClass(module.klass);
		body.add(assign(module.klass.loc, moduleExports, cls));

		var statements = needsLib ? body.finishWithFirstTwo(useStrict, requireNzlib) : body.finishWithFirst(useStrict);
		return new Estree.Program(Loc.zero, statements);
	}

	static Estree.Statement emitImport(Module importer, Module imported) {
		var relPath = importer.fullPath().relTo(imported.fullPath());
		// Must find relative path.
		var pathStr = relPath.withoutExtension(ModuleResolver.extension).toPathString();
		return importStatement(escapeName(imported.name), pathStr);
	}

	static Estree.Statement importStatement(string importedName, string importedPath) {
		var required = Estree.Literal.str(Loc.zero, importedPath);
		var require = Estree.CallExpression.of(Loc.zero, requireId, required);
		return Estree.VariableDeclaration.simple(Loc.zero, importedName, require);
	}

	static readonly Estree.MemberExpression moduleExports = Estree.MemberExpression.simple(Loc.zero, "module", "exports");

	static readonly Estree.MemberExpression objectCreate = Estree.MemberExpression.simple(Loc.zero, "Object", "create");
	Estree.ClassExpression emitClass(ClassDeclaration klass) {
		var loc = klass.loc;
		var name = klass.name;
		var body = Arr.builder<Estree.MethodDefinition>();

		switch (klass.head) {
			case ClassHead.Static _:
			case ClassHead.Abstract _:
				// No constructor
				break;
			case ClassHead.Slots slots:
				body.add(emitSlotsConstructor(slots, needSuperCall: klass.supers.length != 0));
				break;
			default:
				throw TODO();
		}

		var superClass = super(loc, klass.supers);

		foreach (var super in klass.supers)
			foreach (var impl in super.impls)
				body.add(emitMethodOrImpl(impl.loc, name, impl.implemented, impl.body, isStatic: false));

		foreach (var method in klass.methods)
			body.add(emitMethodOrImpl(method.loc, name, method, method.body, method.isStatic));

		return new Estree.ClassExpression(loc, id(loc, escapeName(name)), superClass, new Estree.ClassBody(loc, body.finish()));
	}

	Op<Estree.Expression> super(Loc loc, Arr<Super> supers) {
		switch (supers.length) {
			case 0:
				return Op<Estree.Expression>.None;
			case 1:
				return Op.Some(superClassToExpr(supers.only));
			default:
				needsLib = true;
				return Op.Some<Estree.Expression>(new Estree.CallExpression(loc, JsBuiltins.getMixin(loc), supers.map(superClassToExpr)));
		}
	}
	static Estree.Expression superClassToExpr(Super super) =>
		id(super.loc, escapeName(super.superClass.classDeclaration.name));

	static Estree.MethodDefinition emitSlotsConstructor(ClassHead.Slots s, bool needSuperCall) {
		// constructor(x) { this.x = x; }
		var patterns = s.slots.map<Estree.Pattern>(slot => id(slot.loc, escapeName(slot.name)));
		var first = needSuperCall ? Op.Some(superCall(s.loc)) : Op<Estree.Statement>.None;
		var statements = s.slots.mapWithFirst<Estree.Statement>(first, (slot, i) => {
			var slotLoc = slot.loc;
			var id = (Estree.Identifier)patterns[i];
			var member = Estree.MemberExpression.notComputed(slotLoc, new Estree.ThisExpression(slotLoc), id);
			return assign(slotLoc, member, id);
		});
		return Estree.MethodDefinition.constructor(s.loc, patterns, statements);
	}

	static Estree.Statement superCall(Loc loc) =>
		Estree.ExpressionStatement.of(Estree.CallExpression.of(loc, new Estree.Super(loc)));

	Estree.MethodDefinition emitMethodOrImpl(Loc loc, Sym className, MethodDeclaration method, Expr body, bool isStatic) {
		var @async = isAsync(method);
		var @params = method.parameters.map<Estree.Pattern>(p => id(p.loc, escapeName(p.name)));
		var block = JsExprEmitter.emitMethodBody(ref needsLib, className, @async, body);
		return Estree.MethodDefinition.method(loc, @async, escapeName(method.name), @params, block, isStatic);
	}
}
