using System;

// Note that constructors are always hidden, so don't need this attribute.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class HidAttribute : Attribute {}

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
sealed class JsTranslateAttribute : Attribute {
	internal readonly string builtinMethodName; // Name of a method in JsBuiltins
	internal JsTranslateAttribute(string builtinMethodName) { this.builtinMethodName = builtinMethodName; }
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

		[JsTranslate(nameof(JsBuiltins.eq))]
		public Bool _eq(Bool other) => of(value == other.value);
	}

	[JsPrimitive]
	public sealed class Int : ToData<Int> {
		[JsTranslate(nameof(JsBuiltins.parseInt))]
		public static Int parse(String s) =>
			of(int.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly int value;
		Int(int value) { this.value = value; }

		[Hid] public static Int of(int value) => new Int(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Int>.deepEqual(Int i) => value == i.value;
		Dat ToData<Int>.toDat() => Dat.inum(value);

		[JsTranslate(nameof(JsBuiltins.eq))]
		public Bool _eq(Int other) => Bool.of(value == other.value);

		[JsTranslate(nameof(JsBuiltins.add))]
		public Int _add(Int other) => of(checked(value + other.value));

		[JsTranslate(nameof(JsBuiltins.sub))]
		public Int _sub(Int other) => of(checked(value - other.value));

		[JsTranslate(nameof(JsBuiltins.mul))]
		public Int _mul(Int other) => of(checked(value * other.value));

		[JsTranslate(nameof(JsBuiltins.divInt))]
		public Int _div(Int other) => of(checked(value / other.value));
	}

	[JsPrimitive]
	public sealed class Float : ToData<Float> {
		[JsTranslate(nameof(JsBuiltins.parseFloat))]
		public static Float parse(String s) =>
			of(double.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly double value;

		Float(double value) { this.value = value; }

		[Hid] public static Float of(double d) => new Float(d);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Float>.deepEqual(Float f) => value == f.value;
		Dat ToData<Float>.toDat() => Dat.floatDat(value);

		[JsTranslate(nameof(JsBuiltins.add))]
		public Float _add(Float other) => of(checked(value + other.value));

		[JsTranslate(nameof(JsBuiltins.sub))]
		public Float _sub(Float other) => of(checked(value - other.value));

		[JsTranslate(nameof(JsBuiltins.mul))]
		public Float _mul(Float other) => of(checked(value * other.value));

		[JsTranslate(nameof(JsBuiltins.divFloat))]
		public Float _div(Float other) => of(checked(value / other.value));
	}

	[JsPrimitive]
	public sealed class String : ToData<String> {
		[Hid] internal readonly string value;

		String(string value) { this.value = value; }

		[Hid] public static String of(string value) => new String(value);

		bool DeepEqual<String>.deepEqual(String s) => value == s.value;
		Dat ToData<String>.toDat() => Dat.str(value);

		[JsTranslate(nameof(JsBuiltins.eq))]
		public Bool _eq(String other) => Bool.of(value == other.value);

		[JsTranslate(nameof(JsBuiltins.add))]
		public String _add(String other) => of(value + other.value);
	}

	[HidSuperClass]
	public abstract class Exception : System.Exception {
		protected Exception() : base() {}
		public abstract String description();
	}

	public sealed class AssertionException : Exception {
		public AssertionException() : base() {}
		public override String description() => String.of("Assertion failed.");
	}

	internal static readonly Arr<Type> allTypes = new Arr<Type>(typeof(Builtins).GetNestedTypes());
}
