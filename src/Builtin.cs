using System;

using Model;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class BuiltinNameAttribute : Attribute {
	internal readonly Sym name;
	internal BuiltinNameAttribute(string name) { this.name = Sym.of(name); }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class HidAttribute : Attribute {}

public static class Builtins {
	public sealed class Void : ToData<Void> {
		private Void() {}
		[Hid] public static readonly Void instance = new Void();

		bool DeepEqual<Void>.deepEqual(Void v) => true;
		Dat ToData<Void>.toDat() => Dat.str("<void>");
	}

	public struct Bool : ToData<Bool> {
		[Hid] public readonly bool value;

		[Hid] public static readonly Bool boolTrue = new Bool(true);
		[Hid] public static readonly Bool boolFalse = new Bool(false);

		Bool(bool value) { this.value = value; }

		[Hid] public static Bool of(bool value) => new Bool(value);

		bool DeepEqual<Bool>.deepEqual(Bool b) => value == b.value;
		Dat ToData<Bool>.toDat() => Dat.boolean(value);
	}

	public struct Int : ToData<Int> {
		public static Int parse(Str s) =>
			of(int.Parse(s.value)); //TODO: exceptions

		internal readonly int value;
		Int(int value) { this.value = value; }

		[Hid] public static Int of(int value) => new Int(value);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Int>.deepEqual(Int i) => value == i.value;
		Dat ToData<Int>.toDat() => Dat.inum(value);

		public Int _add(Int other) => of(checked(value + other.value));
		public Int _sub(Int other) => of(checked(value - other.value));
		public Int _mul(Int other) => of(checked(value * other.value));
		public Int _div(Int other) => of(checked(value / other.value));
	}

	public struct Float : ToData<Float> {
		public static Float parse(Str s) =>
			of(double.Parse(s.value)); //TODO: exceptions

		internal readonly double value;

		Float(double value) { this.value = value; }

		[Hid] public static Float of(double d) => new Float(d);

		[Hid] public override string ToString() => value.ToString();

		bool DeepEqual<Float>.deepEqual(Float f) => value == f.value;
		Dat ToData<Float>.toDat() => Dat.floatDat(value);

		public Float _add(Float other) => of(checked(value + other.value));
		public Float _sub(Float other) => of(checked(value - other.value));
		public Float _mul(Float other) => of(checked(value * other.value));
		public Float _div(Float other) => of(checked(value / other.value));
	}

	public struct Str : ToData<Str> {
		internal readonly string value;

		Str(string value) { this.value = value; }

		[Hid] public static Str of(string value) => new Str(value);

		bool DeepEqual<Str>.deepEqual(Str s) => value == s.value;
		Dat ToData<Str>.toDat() => Dat.str(value);

		public Str _add(Str other) => of(value + other.value);
	}

	internal static void register() {
		foreach (var klass in typeof(Builtins).GetNestedTypes())
			BuiltinClass.fromDotNetType(klass); // Also adds it if not already present.
	}
}
