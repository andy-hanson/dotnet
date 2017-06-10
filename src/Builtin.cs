using System;

using Model;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class BuiltinName : Attribute {
    internal readonly Sym name;
    internal BuiltinName(string name) { this.name = Sym.of(name); }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
sealed class Hid : Attribute {}

public static class Builtins {
    public struct Bool {
        [Hid] public readonly bool value;

        [Hid] public static readonly Bool boolTrue = new Bool(true);
        [Hid] public static readonly Bool boolFalse = new Bool(false);

        Bool(bool value) { this.value = value; }

        [Hid]
        public static Bool of(bool value) => new Bool(value);
    }

    public struct Int {
        public static Int parse(Str s) =>
            of(int.Parse(s.value)); //TODO: exceptions

        readonly int value;
        Int(int value) { this.value = value; }

        [Hid]
        public static Int of(int value) => new Int(value);

        public Int _add(Int other) => of(checked (value + other.value));
        public Int _sub(Int other) => of(checked (value - other.value));
        public Int _mul(Int other) => of(checked (value * other.value));
        public Int _div(Int other) => of(checked (value / other.value));

        [Hid]
        public override string ToString() => value.ToString();
    }

    public struct Float {
        public static Float parse(Str s) =>
            of(double.Parse(s.value)); //TODO: exceptions

        readonly double value;

        Float(double value) { this.value = value; }

        [Hid]
        public static Float of(double d) => new Float(d);

        public Float _add(Float other) => of(checked (value + other.value));
        public Float _sub(Float other) => of(checked (value - other.value));
        public Float _mul(Float other) => of(checked (value * other.value));
        public Float _div(Float other) => of(checked (value / other.value));

        [Hid]
        public override string ToString() => value.ToString();
    }

    public struct Str {
        internal readonly string value;

        Str(string value) { this.value = value; }

        [Hid]
        public static Str of(string value) => new Str(value);

        public Str _add(Str other) => of(value + other.value);
    }

    internal static void register() {
        foreach (var klass in typeof(Builtins).GetNestedTypes())
            BuiltinClass.fromDotNetType(klass); // Also adds it if not already present.
    }
}
