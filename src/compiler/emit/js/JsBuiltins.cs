using System;
using System.Reflection;

using Model;
using static EstreeUtils;
using static NameEscaping;
using static Utils;

/**
Treat everything besides `emitInstanceMethodCall` and `emitStaticMethodCall` private.
They are marked internal only so that in Builtin.cs one can write `nameof(JsBuiltins.eq)`.
*/
static class JsBuiltins {
	delegate Estree.Expression InstanceTranslator(Loc loc, Estree.Expression target, Arr<Estree.Expression> args);
	delegate Estree.Expression StaticTranslator(Loc loc, Arr<Estree.Expression> args);

	static readonly Dict<BuiltinMethodWithBody, StaticTranslator> primitiveSpecialTranslateStaticMethods;
	static readonly Dict<BuiltinMethodWithBody, InstanceTranslator> primitiveSpecialTranslateInstanceMethods;
	static readonly Set<BuiltinMethodWithBody> primitiveNzlibStaticMethods;
	static readonly Set<BuiltinMethodWithBody> primitiveNzlibInstanceMethods;

	static JsBuiltins() {
		var primitiveSpecialTranslateStatic = Dict.builder<BuiltinMethodWithBody, StaticTranslator>();
		var primitiveSpecialTranslateInstance = Dict.builder<BuiltinMethodWithBody, InstanceTranslator>();
		var primitiveNzlibStatic = Set.builder<BuiltinMethodWithBody>();
		var primitiveNzlibInstance = Set.builder<BuiltinMethodWithBody>();

		foreach (var k in BuiltinClass.all()) {
			if (!k.dotNetType.hasAttribute<JsPrimitiveAttribute>())
				continue;

			var methods = k.membersMap;
			if (k.supers.length > 0) throw TODO(); // We would have to emit impls as well.

			foreach (var method in methods.values) {
				var builtin = (BuiltinMethodWithBody)method; // Primitives should not have abstract methods, so this cast should succeed.
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
		if (invokedMethod is BuiltinMethodWithBody b) {
			if (primitiveSpecialTranslateStaticMethods.get(b, out var translator))
				return translator(loc, args);
			else if (primitiveNzlibStaticMethods.has(b)) {
				usedNzlib = true;
				return callNzlib(loc, escapeName(b.klass.name), escapeName(b.name), args);
			}
		}

		var access = Estree.MemberExpression.simple(loc, escapeName(invokedMethod.klass.name), escapeName(invokedMethod.name));
		return callPossiblyAsync(loc, isAsync(invokedMethod), access, args);
	}

	internal static Estree.Expression emitInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		if (invokedMethod is BuiltinMethodWithBody b) {
			if (primitiveSpecialTranslateInstanceMethods.get(b, out var translator))
				return translator(loc, target, args);
			else if (primitiveNzlibInstanceMethods.has(b)) {
				usedNzlib = true;
				return callNzlib(loc, escapeName(b.klass.name), escapeName(b.name), args.addLeft(target));
			}
		}

		var member = Estree.MemberExpression.simple(loc, target, escapeName(invokedMethod.name));
		return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
	}

	internal static Estree.Expression emitMyInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		if (invokedMethod is BuiltinMethodWithBody b) {
			unused(usedNzlib);
			throw TODO();
		}

		var member = Estree.MemberExpression.ofThis(loc, escapeName(invokedMethod.name));
		return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
	}

	internal static Estree.Expression incr(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		assert(args.isEmpty);
		return new Estree.BinaryExpression(loc, "+", target, new Estree.Literal(loc, LiteralValue.Nat.of(1)));
	}

	internal static Estree.Expression callToString(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		assert(args.isEmpty);
		return new Estree.CallExpression(loc, Estree.MemberExpression.simple(loc, target, "toString"), Arr.empty<Estree.Expression>());
	}

	internal static Estree.Expression id(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		unused(loc);
		assert(args.isEmpty);
		return target;
	}

	static Estree.Expression callNzlib(Loc loc, string namespaceName, string methodName, Arr<Estree.Expression> args) =>
		new Estree.CallExpression(loc, Estree.MemberExpression.simple(loc, getFromLib(loc, namespaceName), methodName), args);

	static Estree.Expression callNzlib(Loc loc, Arr<Estree.Expression> args, string name) =>
		new Estree.CallExpression(loc, getFromLib(loc, name), args);

	static readonly Estree.Identifier idNzlib = new Estree.Identifier(Loc.zero, "_");

	static readonly Sym symMixin = Sym.of("mixin");
	internal static Estree.Expression getMixin(Loc loc) =>
		getFromLib(loc, "mixin");

	internal static Estree.Expression getAssertionException(Loc loc) =>
		getFromLib(loc, nameof(Builtins.Assertion_Exception));

	internal static Estree.Expression getBuiltin(Loc loc, BuiltinClass b) =>
		getFromLib(loc, escapeName(b.name));

	static Estree.Expression getFromLib(Loc loc, string id) =>
		Estree.MemberExpression.simple(loc, idNzlib, id);

	internal static NzlibData nzlibData() {
		var primitivesBuilder = Dict.builder<BuiltinClass, Arr.Builder<string>>();
		var classes = Dict.builder<string, NzlibClassData>();

		foreach (var k in BuiltinClass.all()) {
			if (k.dotNetType.hasAttribute<JsPrimitiveAttribute>()) {
				primitivesBuilder.add(k, Arr.builder<string>());
				continue;
			}

			var statics = Arr.builder<string>();
			var instance = Arr.builder<string>();

			foreach (var (name, m) in k.membersMap) {
				var method = (Method)m;
				(method.isStatic ? statics : instance).add(escapeName(name));
			}
			foreach (var o in k.overrides)
				instance.add(o.Name);
			//foreach (var s in k.supers)
			//  foreach (var i in s.impls)
			//    instance.add(BuiltinClass.escapeName(i.implemented.name));

			classes.add(escapeName(k.name), new NzlibClassData(statics.finish(), instance.finish()));
		}

		foreach (var p in primitiveNzlibStaticMethods)
			primitivesBuilder[(BuiltinClass)p.klass].add(escapeName(p.name));
		foreach (var p in primitiveNzlibInstanceMethods)
			primitivesBuilder[(BuiltinClass)p.klass].add(escapeName(p.name));
		var primitives = primitivesBuilder.map((klass, names) => (escapeName(klass.name), names.finish()));

		var other = Arr.of(symMixin);

		return new NzlibData(primitives, classes.finish(), other);
	}
}

/** Information about the expected content of nzlib. Used by `testNzlib.js`. */
struct NzlibData : ToData<NzlibData> {
	readonly Dict<string, Arr<string>> primitives;
	readonly Dict<string, NzlibClassData> classes;
	readonly Arr<Sym> other;

	internal NzlibData(Dict<string, Arr<string>> primitives, Dict<string, NzlibClassData> classes, Arr<Sym> other) {
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
