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
	delegate Estree.Expression InstanceTranslator(ref InstanceCtx c);
	delegate Estree.Expression StaticTranslator(ref StaticCtx c);

	/** Treat as private. */
	internal struct InstanceCtx {
		internal bool usedNzlib;
		internal readonly Loc loc;
		internal readonly Estree.Expression target;
		internal readonly Arr<Estree.Expression> args;
		internal InstanceCtx(Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
			usedNzlib = false;
			this.loc = loc;
			this.target = target;
			this.args = args;
		}
	}

	/** Treat as private. */
	internal struct StaticCtx {
		internal bool usedNzlib;
		internal readonly Loc loc;
		internal readonly Arr<Estree.Expression> args;
		internal StaticCtx(Loc loc, Arr<Estree.Expression> args) {
			usedNzlib = false;
			this.loc = loc;
			this.args = args;
		}
	}

	static readonly Dict<Method.BuiltinMethod, InstanceTranslator> specialInstanceMethods;
	static readonly Dict<Method.BuiltinMethod, StaticTranslator> specialStaticMethods;

	static JsBuiltins() {
		var instance = new Dictionary<Method.BuiltinMethod, InstanceTranslator>();
		var statics = new Dictionary<Method.BuiltinMethod, StaticTranslator>();

		foreach (var k in BuiltinClass.all()) {
			var t = k.dotNetType;
			if (t.GetCustomAttribute<JsPrimitiveAttribute>() == null)
				continue;

			var methods = k.membersMap;
			if (k.supers.length > 0) throw TODO(); // We would have to emit impls as well.

			foreach (var method in methods.values) {
				var builtin = (Method.BuiltinMethod)method;
				var attr = builtin.methodInfo.GetCustomAttribute<JsTranslateAttribute>();
				assert(attr != null); // Must add a translator for every method on a class marked JsPrimitive.

				var methodName = attr.builtinMethodName;
				if (builtin.isStatic)
					statics[builtin] = (StaticTranslator)Delegate.CreateDelegate(typeof(StaticTranslator), typeof(JsBuiltins), methodName);
				else
					instance[builtin] = (InstanceTranslator)Delegate.CreateDelegate(typeof(InstanceTranslator), typeof(JsBuiltins), methodName);
			}
		}

		specialInstanceMethods = new Dict<Method.BuiltinMethod, InstanceTranslator>(instance);
		specialStaticMethods = new Dict<Method.BuiltinMethod, StaticTranslator>(statics);
	}

	internal static Estree.Expression emitStaticMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		var ctx = new StaticCtx(loc, args);
		if (invokedMethod is Method.BuiltinMethod b && specialStaticMethods.get(b, out var translator)) {
			var result = translator(ref ctx);
			if (ctx.usedNzlib)
				usedNzlib = true;
			return result;
		} else {
			var access = Estree.MemberExpression.simple(loc, invokedMethod.klass.name, invokedMethod.name);
			return callPossiblyAsync(loc, isAsync(invokedMethod), access, args);
		}
	}

	internal static Estree.Expression emitInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Estree.Expression target, Arr<Estree.Expression> args) {
		var ctx = new InstanceCtx(loc, target, args);
		if (invokedMethod is Method.BuiltinMethod b && specialInstanceMethods.get(b, out var translator)) {
			var result = translator(ref ctx);
			if (ctx.usedNzlib)
				usedNzlib = true;
			return result;
		} else {
			var member = Estree.MemberExpression.simple(loc, target, invokedMethod.name);
			return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
		}
	}

	internal static Estree.Expression emitMyInstanceMethodCall(ref bool usedNzlib, Method invokedMethod, Loc loc, Arr<Estree.Expression> args) {
		if (invokedMethod is Method.BuiltinMethod) {
			unused(usedNzlib);
			throw TODO();
		}

		var member = Estree.MemberExpression.ofThis(loc, invokedMethod.name);
		return callPossiblyAsync(loc, isAsync(invokedMethod), member, args);
	}

	internal static Estree.Expression eq(ref InstanceCtx c) =>
		binary(ref c, "===");

	internal static Estree.Expression add(ref InstanceCtx c) =>
		binary(ref c, "+");

	internal static Estree.Expression sub(ref InstanceCtx c) =>
		binary(ref c, "-");

	internal static Estree.Expression mul(ref InstanceCtx c) =>
		binary(ref c, "*");

	internal static Estree.Expression divInt(ref InstanceCtx c) =>
		callNzlib(ref c, nameof(divInt));

	internal static Estree.Expression divFloat(ref InstanceCtx c) =>
		binary(ref c, "/");

	internal static Estree.Expression parseInt(ref StaticCtx c) => callNzlib(ref c, nameof(parseInt));
	internal static Estree.Expression parseFloat(ref StaticCtx c) => callNzlib(ref c, nameof(parseFloat));

	internal static Estree.Expression callNzlib(ref InstanceCtx c, string name) {
		c.usedNzlib = true;
		return new Estree.CallExpression(c.loc, getFromLib(c.loc, Sym.of(name)), c.args.addLeft(c.target));
	}
	internal static Estree.Expression callNzlib(ref StaticCtx c, string name) {
		c.usedNzlib = true;
		return new Estree.CallExpression(c.loc, getFromLib(c.loc, Sym.of(name)), c.args);
	}

	static Estree.Expression binary(ref InstanceCtx c, string @operator) =>
		new Estree.BinaryExpression(c.loc, @operator, c.target, c.args.only);

	static readonly Estree.Identifier idNzlib = new Estree.Identifier(Loc.zero, Sym.of("_"));
	internal static Estree.Expression getFromLib(Loc loc, Sym id) =>
		Estree.MemberExpression.simple(loc, idNzlib, id);
}
