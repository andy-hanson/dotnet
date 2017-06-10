using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
sealed class BuiltinName : Attribute {
    internal readonly Sym name;
    internal BuiltinName(string name) { this.name = Sym.of(name); }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
sealed class Hid : Attribute {}

struct Int {
    public static Int parse(Str s) =>
        of(int.Parse(s.value)); //TODO: exceptions

    readonly int value;
    Int(int value) { this.value = value; }

    static Int of(int value) => new Int(value);

    public Int _add(Int other) => of(checked (value + other.value));
    public Int _sub(Int other) => of(checked (value - other.value));
    public Int _mul(Int other) => of(checked (value * other.value));
    public Int _div(Int other) => of(checked (value / other.value));

    [Hid]
    public override string ToString() => value.ToString();
}

struct Float {
    public static Float parse(Str s) =>
        of(double.Parse(s.value)); //TODO: exceptions

    readonly double value;

    Float(double value) { this.value = value; }

    static Float of(double d) => new Float(d);

    public Float _add(Float other) => of(checked (value + other.value));
    public Float _sub(Float other) => of(checked (value - other.value));
    public Float _mul(Float other) => of(checked (value * other.value));
    public Float _div(Float other) => of(checked (value / other.value));

    [Hid]
    public override string ToString() => value.ToString();
}

struct Str {
    internal readonly string value;

    Str(string value) { this.value = value; }
    static Str of(string s) => new Str(s);

    public Str _add(Str other) => of(value + other.value);
}
