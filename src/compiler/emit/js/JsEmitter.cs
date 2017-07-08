using Model;

using static EstreeUtils;
using static Utils;

sealed class JsEmitter {
	internal static string emitToString(Module m) {
		var p = new JsEmitter().emitToProgram(m);
		return JsWriter.writeToString(p);
	}

	private bool needsLib = false;

	static Estree.Identifier baseId(string name) => id(Loc.zero, Sym.of(name));
	static readonly Estree.Identifier requireId = baseId("require");
	static readonly Estree.Statement requireNzlib = importStatement(Sym.of("_"), "nzlib");

	Estree.Program emitToProgram(Module m) {
		var body = Arr.builder<Estree.Statement>();

		// Eagerly add lib import. If we don't need this, we'll finishTail()
		body.add(requireNzlib);

		foreach (var import in m.imports)
			body.add(emitImport(m, import));

		var cls = emitClass(m.klass);
		body.add(assign(m.klass.loc, moduleExports, cls));

		return new Estree.Program(Loc.zero, needsLib ? body.finish() : body.finishTail());
	}

	static Estree.Statement emitImport(Module importer, Module imported) {
		var relPath = importer.fullPath().relTo(imported.fullPath());
		// Must find relative path.
		var pathStr = relPath.withoutExtension(ModuleResolver.extension).toPathString();
		return importStatement(imported.name, pathStr);
	}

	static Estree.Statement importStatement(Sym importedName, string importedPath) {
		var required = new Estree.Literal(Loc.zero, LiteralValue.String.of(importedPath));
		var require = Estree.CallExpression.of(Loc.zero, requireId, required);
		return Estree.VariableDeclaration.simple(Loc.zero, importedName, require);
	}

	static readonly Estree.MemberExpression moduleExports = Estree.MemberExpression.simple(Loc.zero, Sym.of("module"), Sym.of("exports"));

	static readonly Estree.MemberExpression objectCreate = Estree.MemberExpression.simple(Loc.zero, Sym.of("Object"), Sym.of("create"));
	Estree.ClassExpression emitClass(Klass klass) {
		var body = Arr.builder<Estree.MethodDefinition>();

		switch (klass.head) {
			case Klass.Head.Static _:
			case Klass.Head.Abstract _:
				// No constructor
				break;
			case Klass.Head.Slots slots:
				body.add(emitSlotsConstructor(slots, needSuperCall: klass.supers.length != 0));
				break;
			default:
				throw TODO();
		}

		var superClass =  super(klass.loc, klass.supers);

		foreach (var super in klass.supers)
			foreach (var impl in super.impls)
				body.add(emitMethodOrImpl(impl.loc, impl.implemented, impl.body, isStatic: false));

		foreach (var method in klass.methods)
			switch (method) {
				case Method.MethodWithBody mb:
					body.add(emitMethodOrImpl(method.loc, method, mb.body, method.isStatic));
					break;
				case Method.AbstractMethod a:
					// These compile to nothing -- they are abstract.
					break;
				default:
					throw unreachable();
			}

		var loc = klass.loc;
		return new Estree.ClassExpression(loc, id(loc, klass.name), superClass, new Estree.ClassBody(loc, body.finish()));
	}

	static readonly Sym idMixin = Sym.of("mixin");
	Op<Estree.Expression> super(Loc loc, Arr<Super> supers) {
		switch (supers.length) {
			case 0:
				return Op<Estree.Expression>.None;
			case 1:
				return Op.Some(superClassToExpr(supers.only));
			default:
				needsLib = true;
				var mixin = JsBuiltins.getFromLib(loc, idMixin);
				return Op.Some<Estree.Expression>(new Estree.CallExpression(loc, mixin, supers.map(superClassToExpr)));
		}
	}
	static Estree.Expression superClassToExpr(Super super) =>
		id(super.loc, super.superClass.name);

	static Estree.MethodDefinition emitSlotsConstructor(Klass.Head.Slots s, bool needSuperCall) {
		// constructor(x) { this.x = x; }
		var patterns = s.slots.map<Estree.Pattern>(slot => id(slot.loc, slot.name));
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

	Estree.MethodDefinition emitMethodOrImpl(Loc loc, Method method, Expr body, bool isStatic) {
		var async = isAsync(method);
		return Estree.MethodDefinition.method(loc, async, method.name, method.parameters.map(emitParameter), exprToBlockStatement(async, body), isStatic);
	}

	Estree.BlockStatement exprToBlockStatement(bool async, Expr body) {
		var me = new JsExprEmitter(async);
		var res = me.exprToBlockStatement(body);
		if (me.needsLib) needsLib = true;
		return res;
	}

	static Estree.Pattern emitParameter(Parameter p) => id(p.loc, p.name);
}
