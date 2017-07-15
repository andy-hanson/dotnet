using System;
using System.Reflection;

using Model;
using static EstreeUtils;
using static Utils;

/**
Treat everything besides `emitInstanceMethodCall` and `emitStaticMethodCall` private.
They are marked internal only so that in Builtin.cs one can write `nameof(JsBuiltins.eq)`.
*/
static class JsBuiltins {
	delegate Estree.Expression InstanceTranslator(Loc loc, Estree.Expression target, Arr<Estree.Expression> args);
	delegate Estree.Expression StaticTranslator(Loc loc, Arr<Estree.Expression> args);

	static readonly Dict<Method.BuiltinMethod, StaticTranslator> primitiveSpecialTranslateStaticMethods;
	static readonly Dict<Method.BuiltinMethod, InstanceTranslator> primitiveSpecialTranslateInstanceMethods;
	static readonly Set<Method.BuiltinMethod> primitiveNzlibStaticMethods;
	static readonly Set<Method.BuiltinMethod> primitiveNzlibInstanceMethods;

	static JsBuiltins() {
		var primitiveSpecialTranslateStatic = Dict.builder<Method.BuiltinMethod, StaticTranslator>();
		var primitiveSpecialTranslateInstance = Dict.builder<Method.BuiltinMethod, InstanceTranslator>();
		var primitiveNzlibStatic = Set.builder<Method.BuiltinMethod>();
		var primitiveNzlibInstance = Set.builder<Method.BuiltinMethod>();

		foreach (var k in BuiltinClass.all()) {
			if (!k.dotNetType.hasAttribute<JsPrimitiveAttribute>())
				continue;

			var methods = k.membersMap;
			if (k.supers.length > 0) throw TODO(); // We would have to emit impls as well.

			foreach (var method in methods.values) {
				var builtin = (Method.BuiltinMethod)method;
				var attr = builtin.methodInfo.GetCustomAttribute<AnyJsTranslateAttribute>();
				var isStatic = builtin.isStatic;

				switch (attr) {
					case JsSpecialTranslateAttribute j:
						var methodName = j.builtinMethodName;
						if (isStatic) {
							var del = (StaticTranslator)Delegate.CreateDelegate(typeof(StaticTranslator), typeof(JsBuiltins), methodName);
							primitiveSpecialTranslateStatic.add(builtin, del);
						}
						else {
							var del = (InstanceTranslator)Delegate.CreateDelegate(typeof(InstanceTranslator), typeof(JsBuiltins), methodName);
							primitiveSpecialTranslateInstance.add(builtin, del);
						}
						break;
					case JsBinaryAttribute b:
						var @operator = b.@operator;
						if (isStatic)
							primitiveSpecialTranslateStatic.add(builtin, (loc, args) => {
								assert(args.length == 2);
								return new Estree.BinaryExpression(loc, @operator, args[0], args[1]);
							});
						else
							primitiveSpecialTranslateInstance.add(builtin, (loc, target, args) =>
								new Estree.BinaryExpression(loc, @operator, target, args.only));
						break;
					case null:
						(isStatic ? primitiveNzlibStatic : primitiveNzlibInstance).add(builtin);
						break;
					default:
						throw unreachable();
				}
			}
		}

		primitiveSpecialTranslateStaticMethods = primitiveSpecialTranslateStatic.finish();
		primitiveSpecialTranslateInstanceMethods = primitiveSpecialTranslateInstance.finish();
		primitiveNzlibStaticMethods = primitiveNzlibStatic.finish();
		primitiveNzlibInstanceMethods = primitiveNzlibInstance.finish();
	}

	internal static Estree.Expression emitStaticMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod b) {
			if (primitiveSpecialTranslateStaticMethods.get(b, out var translator))
				return translator(loc, args);
			else if (primitiveNzlibStaticMethods.has(b)) {
				usedNzlib = true;
				return callNzlib(loc, b.klass.name, b.name, args);
			}
		}

		var access = Estree.MemberExpression.simple(loc, invokedMethod.klass.name, invokedMethod.name);
		return callPossiblyAsync(loc, isAsync(invokedMethod), access, args);
	}

	internal static Estree.Expression emitInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod b) {
			if (primitiveSpecialTranslateInstanceMethods.get(b, out var translator))
				return translator(loc, target, args);
			else if (primitiveNzlibInstanceMethods.has(b)) {
				usedNzlib = true;
				return callNzlib(loc, b.klass.name, b.name, args.addLeft(target));
			}
		}

		var member = Estree.MemberExpression.simple(loc, target, invokedMethod.name);
		return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
	}

	internal static Estree.Expression emitMyInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod) {
			unused(usedNzlib);
			throw TODO();
		}

		var member = Estree.MemberExpression.ofThis(loc, invokedMethod.name);
		return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
	}

	static readonly Sym symToString = Sym.of("toString");
	internal static Estree.Expression toString(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		assert(args.isEmpty);
		return new Estree.CallExpression(loc, Estree.MemberExpression.simple(loc, target, symToString), Arr.empty<Estree.Expression>());
	}

	internal static Estree.Expression id(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		unused(loc);
		assert(args.isEmpty);
		return target;
	}

	static Estree.Expression callNzlib(Loc loc, Sym namespaceName, Sym methodName, Arr<Estree.Expression> args) =>
		new Estree.CallExpression(loc, Estree.MemberExpression.simple(loc, getFromLib(loc, namespaceName), methodName), args);

	static Estree.Expression callNzlib(Loc loc, Arr<Estree.Expression> args, string name) =>
		new Estree.CallExpression(loc, getFromLib(loc, Sym.of(name)), args);

	static readonly Estree.Identifier idNzlib = new Estree.Identifier(Loc.zero, Sym.of("_"));

	static readonly Sym symMixin = Sym.of("mixin");
	internal static Estree.Expression getMixin(Loc loc) =>
		getFromLib(loc, symMixin);

	static readonly Sym symAssertionException = Sym.of(nameof(Builtins.AssertionException));

	internal static Estree.Expression getAssertionException(Loc loc) =>
		getFromLib(loc, symAssertionException);

	internal static Estree.Expression getBuiltin(Loc loc, BuiltinClass b) =>
		getFromLib(loc, b.name);

	static Estree.Expression getFromLib(Loc loc, Sym id) =>
		Estree.MemberExpression.simple(loc, idNzlib, id);

	internal static NzlibData nzlibData() {
		var primitivesBuilder = Dict.builder<BuiltinClass, Arr.Builder<string>>();
		var classes = Dict.builder<Sym, NzlibClassData>();

		foreach (var k in BuiltinClass.all()) {
			if (k.dotNetType.hasAttribute<JsPrimitiveAttribute>()) {
				primitivesBuilder.add(k, Arr.builder<string>());
				continue;
			}

			var statics = Arr.builder<string>();
			var instance = Arr.builder<string>();

			foreach (var (name, m) in k.membersMap) {
				var method = (Method) m;
				(method.isStatic ? statics : instance).add(BuiltinClass.escapeName(name));
			}
			foreach (var o in k.overrides)
				instance.add(o.Name);
			//foreach (var s in k.supers)
			//	foreach (var i in s.impls)
			//		instance.add(BuiltinClass.escapeName(i.implemented.name));

			classes.add(k.name, new NzlibClassData(statics.finish(), instance.finish()));
		}

		foreach (var p in primitiveNzlibStaticMethods)
			primitivesBuilder[(BuiltinClass)p.klass].add(BuiltinClass.escapeName(p.name));
		foreach (var p in primitiveNzlibInstanceMethods)
			primitivesBuilder[(BuiltinClass)p.klass].add(BuiltinClass.escapeName(p.name));
		var primitives = primitivesBuilder.map((klass, names) => (klass.name, names.finish()));

		var other = Arr.of(symMixin);

		return new NzlibData(primitives, classes.finish(), other);
	}
}

/** Information about the expected content of nzlib. Used by `testNzlib.js`. */
struct NzlibData : ToData<NzlibData> {
	readonly Dict<Sym, Arr<string>> primitives;
	readonly Dict<Sym, NzlibClassData> classes;
	readonly Arr<Sym> other;

	internal NzlibData(Dict<Sym, Arr<string>> primitives, Dict<Sym, NzlibClassData> classes, Arr<Sym> other) {
		this.primitives = primitives;
		this.classes = classes;
		this.other = other;
	}

	bool DeepEqual<NzlibData>.deepEqual(NzlibData other) => throw new NotSupportedException();
	Dat ToData<NzlibData>.toDat() => Dat.of(this,
		nameof(primitives), Dat.dict(primitives),
		nameof(classes), Dat.dict(classes),
		nameof(other), Dat.arr(other));
}

struct NzlibClassData : ToData<NzlibClassData> {
	readonly Arr<string> statics;
	readonly Arr<string> instance;

	internal NzlibClassData(Arr<string> statics, Arr<string> instance) {
		this.statics = statics;
		this.instance = instance;
	}

	bool DeepEqual<NzlibClassData>.deepEqual(NzlibClassData other) => throw new NotSupportedException();
	Dat ToData<NzlibClassData>.toDat() => Dat.of(this, nameof(statics), Dat.arr(statics), nameof(instance), Dat.arr(instance));
}
