using System;

// Note that constructors are always hidden, so don't need this attribute.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class HidAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Method)]
abstract class HasEffectAttribute : Attribute {
	internal abstract Model.Effect effect { get; }
}

sealed class PureAttribute : HasEffectAttribute {
	internal override Model.Effect effect => Model.Effect.Pure;
}
sealed class GetAttribute : HasEffectAttribute {
	internal override Model.Effect effect => Model.Effect.Get;
}
sealed class SetAttribute : HasEffectAttribute {
	internal override Model.Effect effect => Model.Effect.Set;
}
sealed class IoAttribute : HasEffectAttribute {
	internal override Model.Effect effect => Model.Effect.Io;
}


[AttributeUsage(AttributeTargets.Method)]
sealed class InstanceAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Class)]
sealed class HidSuperClassAttribute : Attribute {}

/**
Attribute for types that are implemented by JS primitives, not by a JS class.
For all other classes, there should be an equivalent version in nzlib.

Methods in a JSPrimitive class will be implemented by functions in `nzlib/primitive`, or have a JsTranslate annotation for special handling.
*/
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
sealed class JsPrimitiveAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Method)]
abstract class AnyJsTranslateAttribute : Attribute {}

sealed class JsSpecialTranslateAttribute : AnyJsTranslateAttribute {
	internal readonly string builtinMethodName; // Name of a method in JsBuiltins
	internal JsSpecialTranslateAttribute(string builtinMethodName) { this.builtinMethodName = builtinMethodName; }
}

sealed class JsBinaryAttribute : AnyJsTranslateAttribute {
	internal readonly string @operator;
	internal JsBinaryAttribute(string @operator) { this.@operator = @operator; }
}

public static class Builtins {
	[JsPrimitive] // Represented by `undefined`
	public sealed class Void : ToData<Void> {
		private Void() {}
		[Hid] public static readonly Void instance = new Void();

		bool DeepEqual<Void>.deepEqual(Void v) => true;
		Dat ToData<Void>.toDat() => Dat.str("<void>");
	}

	[JsPrimitive]
	public struct Bool : ToData<Bool> {
		[Hid] public readonly bool value;

		[Hid] public static readonly Bool boolTrue = new Bool(true);
		[Hid] public static readonly Bool boolFalse = new Bool(false);

		Bool(bool value) { this.value = value; }

		[Hid] public static Bool of(bool value) => new Bool(value);

		bool DeepEqual<Bool>.deepEqual(Bool b) => value == b.value;
		Dat ToData<Bool>.toDat() => Dat.boolean(value);

		[Instance, Pure, JsBinary("===")]
		public static Bool _eq(Bool a, Bool b) => of(a.value == b.value);

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.toString))]
		public static String str(Bool b) => String.of(b.value.ToString());
	}

	[JsPrimitive]
	public struct Nat : ToData<Nat> {
		[Hid] internal readonly uint value;
		Nat(uint value) { this.value = value; }

		[Hid] public static Nat of(uint value) => new Nat(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Nat>.deepEqual(Nat n) => value == n.value;
		Dat ToData<Nat>.toDat() => Dat.nat(value);

		[Instance, Pure, JsBinary("===")]
		public static Bool _eq(Nat a, Nat b) => Bool.of(a.value == b.value);

		[Instance, Pure, JsBinary("+")]
		public static Nat _add(Nat a, Nat b) => of(checked(a.value + b.value));

		[Instance, Pure, JsBinary("-")]
		public static Int _sub(Nat a, Nat b) =>
			Int.of(checked((int)a.value - (int)b.value)); //TODO: there should be a better way of doing this without inner casts.

		[Instance, Pure, JsBinary("*")]
		public static Nat _mul(Nat a, Nat b) => Nat.of(checked(a.value * b.value));

		[Instance, Pure]
		public static Nat _div(Nat a, Nat b) => Nat.of(checked(a.value / b.value));

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.toString))]
		public static String str(Nat n) => String.of(n.value.ToString());

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Int toInt(Nat n) => Int.of(checked((int)n.value));

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Real toReal(Nat n) => Real.of((double)n.value);
	}

	[JsPrimitive]
	public struct Int : ToData<Int> {
		[Pure]
		public static Int parse(String s) =>
			of(int.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly int value;
		Int(int value) { this.value = value; }

		[Hid] public static Int of(int value) => new Int(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Int>.deepEqual(Int i) => value == i.value;
		Dat ToData<Int>.toDat() => Dat.@int(value);

		[Instance, Pure, JsBinary("===")]
		public static Bool _eq(Int a, Int b) => Bool.of(a.value == b.value);

		[Instance, Pure, JsBinary("+")]
		public static Int _add(Int a, Int b) => of(checked(a.value + b.value));

		[Instance, Pure, JsBinary("-")]
		public static Int _sub(Int a, Int b) => of(checked(a.value - b.value));

		[Instance, Pure, JsBinary("*")]
		public static Int _mul(Int a, Int b) => of(checked(a.value * b.value));

		[Instance, Pure]
		public static Int _div(Int a, Int b) => of(checked(a.value / b.value));

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.toString))]
		public static String str(Int i) => String.of(i.value.ToString());

		[Instance, Pure]
		public static Nat toNat(Int i) => Nat.of(checked((uint)i.value));

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Real toReal(Int i) => Real.of((double)i.value);
	}

	[JsPrimitive]
	public struct Real : ToData<Real> {
		[Pure]
		public static Real parse(String s) =>
			of(double.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly double value;

		Real(double value) { this.value = value; }

		[Hid] public static Real of(double d) => new Real(d);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Real>.deepEqual(Real f) => value == f.value;
		Dat ToData<Real>.toDat() => Dat.realDat(value);

		[Instance, Pure, JsBinary("+")]
		public static Real _add(Real a, Real b) => of(checked(a.value + b.value));

		[Instance, Pure, JsBinary("-")]
		public static Real _sub(Real a, Real b) => of(checked(a.value - b.value));

		[Instance, Pure, JsBinary("*")]
		public static Real _mul(Real a, Real b) => of(checked(a.value * b.value));

		[Instance, Pure, JsBinary("/")]
		public static Real _div(Real a, Real b) => of(checked(a.value / b.value));

		[Instance, Pure, JsSpecialTranslate(nameof(JsBuiltins.toString))]
		public static String str(Real r) => String.of(r.value.ToString());

		[Instance, Pure]
		public static Int round(Real r) => Int.of(checked((int)Math.Round(r.value)));

		[Instance, Pure]
		public static Int roundDown(Real r) => Int.of(checked((int)Math.Floor(r.value)));

		[Instance, Pure]
		public static Int roundUp(Real r) => Int.of(checked((int)Math.Ceiling(r.value)));
	}

	[JsPrimitive]
	public struct String : ToData<String> {
		[Hid] internal readonly string value;

		String(string value) { this.value = value; }

		[Hid] public static String of(string value) => new String(value);

		bool DeepEqual<String>.deepEqual(String s) => value == s.value;
		Dat ToData<String>.toDat() => Dat.str(value);

		[Instance, Pure, JsBinary("===")]
		public static Bool _eq(String a, String b) => Bool.of(a.value == b.value);

		[Instance, Pure, JsBinary("+")]
		public static String _add(String a, String b) => of(a.value + b.value);
	}

	[HidSuperClass]
	public abstract class Exception : System.Exception {
		protected Exception() : base() {}
		[Pure] public abstract String description();
	}

	public sealed class AssertionException : Exception {
		public AssertionException() : base() {}
		public override String description() => String.of("Assertion failed.");
	}

	public interface Console {
		[Io] Void write_line(String s);
	}

	internal static readonly Arr<Type> allTypes = new Arr<Type>(typeof(Builtins).GetNestedTypes());
}
