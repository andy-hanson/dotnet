using System;
using System.Collections.Generic;
using System.Reflection;

using static Utils;

static class JsConvert {
	internal static Type mpType(Type t) =>
		convertersToJs.ContainsKey(t) ? typeof(Jint.Native.JsValue) : t;

	internal static Jint.Native.JsValue toJsValue(object o) {
		switch (o) {
			case Builtins.Void v:
				return __Converters.__voidToJs(v);
			case Builtins.Bool b:
				return __Converters.__boolToJs(b);
			case Builtins.Int i:
				return __Converters.__intToJs(i);
			case Builtins.Float f:
				return __Converters.__floatToJs(f);
			case Builtins.Str s:
				return __Converters.__strToJs(s);
			default:
				throw TODO();
		}
	}

	static readonly Dictionary<Type, MethodInfo> convertersToJs = new Dictionary<Type, string> {
		{ typeof(Builtins.Void), nameof(__Converters.__voidToJs) },
		{ typeof(Builtins.Bool), nameof(__Converters.__boolToJs) },
		{ typeof(Builtins.Int), nameof(__Converters.__intToJs) },
		{ typeof(Builtins.Float), nameof(__Converters.__floatToJs) },
		{ typeof(Builtins.Str), nameof(__Converters.__strToJs) }
	}.mapValuesToDictionary(typeof(__Converters).GetMethod);

	internal static bool converterToJs(Type t, out MethodInfo m) => convertersToJs.TryGetValue(t, out m);

	static readonly Dictionary<Type, MethodInfo> convertersFromJs = new Dictionary<Type, string> {
		{ typeof(Builtins.Void), nameof(__Converters.__voidFromJs) },
		{ typeof(Builtins.Bool), nameof(__Converters.__boolFromJs) },
		{ typeof(Builtins.Int), nameof(__Converters.__intFromJs) },
		{ typeof(Builtins.Float), nameof(__Converters.__floatFromJs) },
		{ typeof(Builtins.Str), nameof(__Converters.__strFromJs) }
	}.mapValuesToDictionary(typeof(__Converters).GetMethod);

	internal static bool converterFromJs(Type t, out MethodInfo m) => convertersFromJs.TryGetValue(t, out m);
}

/**
NOT public, treat as private to JsConvert.
Needs to be public so dynamically-created assembly can use this.
*/
public static class __Converters {
	public static Jint.Native.JsValue __voidToJs(Builtins.Void v) {
		unused(v);
		return Jint.Native.JsValue.Undefined;
	}
	public static Jint.Native.JsValue __boolToJs(Builtins.Bool b) =>
		b.value ? Jint.Native.JsValue.True : Jint.Native.JsValue.False;
	public static Jint.Native.JsValue __intToJs(Builtins.Int i) =>
		new Jint.Native.JsValue(i.value);
	public static Jint.Native.JsValue __floatToJs(Builtins.Float f) =>
		new Jint.Native.JsValue(f.value);
	public static Jint.Native.JsValue __strToJs(Builtins.Str s) =>
		new Jint.Native.JsValue(s.value);

	public static Builtins.Void __voidFromJs(Jint.Native.JsValue j) {
		assert(j == Jint.Native.JsValue.Undefined);
		return Builtins.Void.instance;
	}
	public static Builtins.Bool __boolFromJs(Jint.Native.JsValue j) =>
		Builtins.Bool.of(j.AsBoolean());
	public static Builtins.Int __intFromJs(Jint.Native.JsValue j) {
		var n = j.AsNumber();
		assert(n % 1 == 0);
		return Builtins.Int.of((int)n);
	}
	public static Builtins.Float __floatFromJs(Jint.Native.JsValue j) =>
		Builtins.Float.of(j.AsNumber());
	public static Builtins.Str __strFromJs(Jint.Native.JsValue j) =>
		Builtins.Str.of(j.AsString());
}
