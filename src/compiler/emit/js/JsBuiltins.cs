using System;
using System.Collections.Generic;
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

	static readonly Dict<Method.BuiltinMethod, StaticTranslator> specialStaticMethods;
	static readonly Dict<Method.BuiltinMethod, InstanceTranslator> specialInstanceMethods;
	static readonly Dict<Method.BuiltinMethod, string> nzlibStaticMethods;
	static readonly Dict<Method.BuiltinMethod, string> nzlibInstanceMethods;

	static JsBuiltins() {
		var specialStatic = Dict.builder<Method.BuiltinMethod, StaticTranslator>();
		var specialInstance = Dict.builder<Method.BuiltinMethod, InstanceTranslator>();
		var nzlibStatic = Dict.builder<Method.BuiltinMethod, string>();
		var nzlibInstance = Dict.builder<Method.BuiltinMethod, string>();

		foreach (var k in BuiltinClass.all()) {
			var t = k.dotNetType;
			if (t.GetCustomAttribute<JsPrimitiveAttribute>() == null)
				continue;

			var methods = k.membersMap;
			if (k.supers.length > 0) throw TODO(); // We would have to emit impls as well.

			foreach (var method in methods.values) {
				var builtin = (Method.BuiltinMethod)method;
				var attr = builtin.methodInfo.GetCustomAttribute<AnyJsTranslateAttribute>();
				assert(attr != null); // Must add a translator for every method on a class marked JsPrimitive.

				var isStatic = builtin.isStatic;

				switch (attr) {
					case JsSpecialTranslateAttribute j:
						var methodName = j.builtinMethodName;
						if (isStatic)
							specialStatic.add(builtin, (StaticTranslator)Delegate.CreateDelegate(typeof(StaticTranslator), typeof(JsBuiltins), methodName));
						else
							specialInstance.add(builtin, (InstanceTranslator)Delegate.CreateDelegate(typeof(InstanceTranslator), typeof(JsBuiltins), methodName));
						break;
					case NzlibAttribute n:
						(isStatic ? nzlibStatic : nzlibInstance).add(builtin, n.nzlibFunctionName);
						break;
					case JsBinaryAttribute b:
						var @operator = b.@operator;
						if (isStatic)
							specialStatic.add(builtin, (loc, args) => {
								assert(args.length == 2);
								return new Estree.BinaryExpression(loc, @operator, args[0], args[1]);
							});
						else
							specialInstance.add(builtin, (loc, target, args) =>
								new Estree.BinaryExpression(loc, @operator, target, args.only));
						break;
					default:
						throw unreachable();
				}
			}
		}

		specialStaticMethods = specialStatic.finish();
		specialInstanceMethods = specialInstance.finish();
		nzlibStaticMethods = nzlibStatic.finish();
		nzlibInstanceMethods = nzlibInstance.finish();
	}

	internal static Estree.Expression emitStaticMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod b) {
			if (specialStaticMethods.get(b, out var translator))
				return translator(loc, args);
			else if (nzlibStaticMethods.get(b, out var name)) {
				usedNzlib = true;
				return callNzlib(loc, args, name);
			}
		}

		var access = Estree.MemberExpression.simple(loc, invokedMethod.klass.name, invokedMethod.name);
		return callPossiblyAsync(loc, isAsync(invokedMethod), access, args);
	}

	internal static Estree.Expression emitInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod b) {
			if (specialInstanceMethods.get(b, out var translator))
				return translator(loc, target, args);
			else if (nzlibInstanceMethods.get(b, out var name)) {
				usedNzlib = true;
				return callNzlib(loc, args.addLeft(target), name);
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
	internal static Estree.Expression str(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		assert(args.isEmpty);
		return new Estree.CallExpression(loc, Estree.MemberExpression.simple(loc, target, symToString), Arr.empty<Estree.Expression>());
	}

	internal static Estree.Expression id(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		unused(loc);
		assert(args.isEmpty);
		return target;
	}

	static Estree.Expression callNzlib(Loc loc, Arr<Estree.Expression> args, string name) =>
		new Estree.CallExpression(loc, getFromLib(loc, Sym.of(name)), args);

	static readonly Estree.Identifier idNzlib = new Estree.Identifier(Loc.zero, Sym.of("_"));
	internal static Estree.Expression getFromLib(Loc loc, Sym id) =>
		Estree.MemberExpression.simple(loc, idNzlib, id);
}
