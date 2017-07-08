using Model;

abstract class LiteralValue : ToData<LiteralValue> {
	internal abstract Ty ty { get; }
	LiteralValue() {}
	public abstract bool deepEqual(LiteralValue l);
	public abstract Dat toDat();

	internal sealed class Pass : LiteralValue, ToData<Pass> {
		Pass() {}
		internal static readonly Pass instance = new Pass();
		internal override Ty ty => BuiltinClass.Void;
		public override bool deepEqual(LiteralValue l) => l == instance;
		public bool deepEqual(Pass p) => true;
		public override Dat toDat() => Dat.of(this);
	}

	internal sealed class Bool : LiteralValue, ToData<Bool> {
		internal readonly bool value;
		Bool(bool value) { this.value = value; }
		internal static readonly Bool @true = new Bool(true);
		internal static readonly Bool @false = new Bool(false);
		internal static Bool of(bool b) => b ? @true : @false;

		internal override Ty ty => BuiltinClass.Bool;

		public override bool deepEqual(LiteralValue l) => l is Bool b && deepEqual(b);
		public bool deepEqual(Bool b) => value == b.value;
		public override Dat toDat() => Dat.boolean(value);
	}

	internal sealed class Int : LiteralValue {
		internal readonly int value;
		Int(int value) { this.value = value; }
		internal static Int of(int value) => new Int(value); //TODO: cache common values

		internal override Ty ty => BuiltinClass.Int;

		public override bool deepEqual(LiteralValue l) => l is Int i && deepEqual(i);
		public bool deepEqual(Int i) => value == i.value;
		public override Dat toDat() => Dat.inum(value);
	}

	internal sealed class Float : LiteralValue {
		internal readonly double value;
		Float(double value) { this.value = value; }
		internal static Float of(double value) => new Float(value);

		internal override Ty ty => BuiltinClass.Float;

		public override bool deepEqual(LiteralValue l) => l is Float f && deepEqual(f);
		public bool deepEqual(Float f) => value == f.value;
		public override Dat toDat() => Dat.floatDat(value);
	}

	internal sealed class String : LiteralValue {
		internal readonly string value;
		String(string value) { this.value = value; }
		internal static String of(string value) => new String(value);

		internal override Ty ty => BuiltinClass.String;

		public override bool deepEqual(LiteralValue l) => l is String s && deepEqual(s);
		public bool deepEqual(String s) => value == s.value;
		public override Dat toDat() => Dat.str(value);
	}
}
