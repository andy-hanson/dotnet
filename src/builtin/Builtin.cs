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

[AttributeUsage(AttributeTargets.Class)]
sealed class HidSuperClassAttribute : Attribute {}

/**
Attribute for types that are implemented by JS primitives, not by a JS class.
Every method in these classes must have a JsTranslate annotation.

For all other classes, there should be an equivalent version in nzlib.
*/
[AttributeUsage(AttributeTargets.Class)]
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

sealed class NzlibAttribute : AnyJsTranslateAttribute {
	internal readonly string nzlibFunctionName; // Name of a function exported by nzlib
	internal NzlibAttribute(string nzlibFunctionName) { this.nzlibFunctionName = nzlibFunctionName; }
}

// TODO: Most of these should be structs.
// But see https://github.com/dotnet/coreclr/issues/12596
public static class Builtins {
	[JsPrimitive] // Represented by `undefined`
	public sealed class Void : ToData<Void> {
		private Void() {}
		[Hid] public static readonly Void instance = new Void();

		bool DeepEqual<Void>.deepEqual(Void v) => true;
		Dat ToData<Void>.toDat() => Dat.str("<void>");
	}

	[JsPrimitive]
	public sealed class Bool : ToData<Bool> {
		[Hid] public readonly bool value;

		[Hid] public static readonly Bool boolTrue = new Bool(true);
		[Hid] public static readonly Bool boolFalse = new Bool(false);

		Bool(bool value) { this.value = value; }

		[Hid] public static Bool of(bool value) => new Bool(value);

		bool DeepEqual<Bool>.deepEqual(Bool b) => value == b.value;
		Dat ToData<Bool>.toDat() => Dat.boolean(value);

		[Pure, JsBinary("===")]
		public Bool _eq(Bool other) => of(value == other.value);

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.str))]
		public String str() => String.of(value.ToString());
	}

	[JsPrimitive]
	public sealed class Nat : ToData<Nat> {
		[Hid] internal readonly uint value;
		Nat(uint value) { this.value = value; }

		[Hid] public static Nat of(uint value) => new Nat(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Nat>.deepEqual(Nat n) => value == n.value;
		Dat ToData<Nat>.toDat() => Dat.nat(value);

		[Pure, JsBinary("===")]
		public Bool _eq(Nat other) => Bool.of(value == other.value);

		[Pure, JsBinary("+")]
		public Nat _add(Nat other) => of(checked(value + other.value));

		[Pure, JsBinary("-")]
		public Int _sub(Nat other) =>
			Int.of(checked((int)value - (int)other.value)); //TODO: there should be a better way of doing this without inner casts.

		[Pure, JsBinary("*")]
		public Nat _mul(Nat other) => Nat.of(checked(value * other.value));

		[Pure, Nzlib("divInt")]
		public Nat _div(Nat other) => Nat.of(checked(value / other.value));

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.str))]
		public String str() => String.of(value.ToString());

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public Int toInt() => Int.of(checked((int)value));

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public Real toReal() => Real.of((double)value);
	}

	[JsPrimitive]
	public sealed class Int : ToData<Int> {
		[Pure, Nzlib("parseInt")]
		public static Int parse(String s) =>
			of(int.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly int value;
		Int(int value) { this.value = value; }

		[Hid] public static Int of(int value) => new Int(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Int>.deepEqual(Int i) => value == i.value;
		Dat ToData<Int>.toDat() => Dat.@int(value);

		[Pure, JsBinary("===")]
		public Bool _eq(Int other) => Bool.of(value == other.value);

		[Pure, JsBinary("+")]
		public Int _add(Int other) => of(checked(value + other.value));

		[Pure, JsBinary("-")]
		public Int _sub(Int other) => of(checked(value - other.value));

		[Pure, JsBinary("*")]
		public Int _mul(Int other) => of(checked(value * other.value));

		[Pure, Nzlib("divInt")]
		public Int _div(Int other) => of(checked(value / other.value));

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.str))]
		public String str() => String.of(value.ToString());

		[Pure, Nzlib(nameof(toNat))]
		public Nat toNat() => Nat.of(checked((uint)value));

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public Real toReal() => Real.of((double)value);
	}

	[JsPrimitive]
	public sealed class Real : ToData<Real> {
		[Pure, Nzlib("parseReal")]
		public static Real parse(String s) =>
			of(double.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly double value;

		Real(double value) { this.value = value; }

		[Hid] public static Real of(double d) => new Real(d);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Real>.deepEqual(Real f) => value == f.value;
		Dat ToData<Real>.toDat() => Dat.realDat(value);

		[Pure, JsBinary("+")]
		public Real _add(Real other) => of(checked(value + other.value));

		[Pure, JsBinary("-")]
		public Real _sub(Real other) => of(checked(value - other.value));

		[Pure, JsBinary("*")]
		public Real _mul(Real other) => of(checked(value * other.value));

		[Pure, JsBinary("/")]
		public Real _div(Real other) => of(checked(value / other.value));

		[Pure, JsSpecialTranslate(nameof(JsBuiltins.str))]
		public String str() => String.of(value.ToString());

		[Pure, Nzlib(nameof(round))]
		public Int round() => Int.of(checked((int)Math.Round(value)));

		[Pure, Nzlib(nameof(roundDown))]
		public Int roundDown() => Int.of(checked((int)Math.Floor(value)));

		[Pure, Nzlib(nameof(roundUp))]
		public Int roundUp() => Int.of(checked((int)Math.Ceiling(value)));
	}

	[JsPrimitive]
	public sealed class String : ToData<String> {
		[Hid] internal readonly string value;

		String(string value) { this.value = value; }

		[Hid] public static String of(string value) => new String(value);

		bool DeepEqual<String>.deepEqual(String s) => value == s.value;
		Dat ToData<String>.toDat() => Dat.str(value);

		[Pure, JsBinary("===")]
		public Bool _eq(String other) => Bool.of(value == other.value);

		[Pure, JsBinary("+")]
		public String _add(String other) => of(value + other.value);
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
