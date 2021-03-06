using System;

using BuiltinAttributes;
using static Utils;

public static class Builtins {
	[NoImport, JsPrimitive] // Represented by `undefined`
	public sealed class Void : ToData<Void> {
		private Void() {}
		[Hid] public static readonly Void instance = new Void();

		bool DeepEqual<Void>.deepEqual(Void v) => true;
		Dat ToData<Void>.toDat() => Dat.str("<void>");
	}

	[NoImport, JsPrimitive]
	public struct Bool : ToData<Bool> {
		[Hid] public readonly bool value;

		[Hid] public static readonly Bool boolTrue = new Bool(true);
		[Hid] public static readonly Bool boolFalse = new Bool(false);

		Bool(bool value) { this.value = value; }

		[Hid] public static Bool of(bool value) => new Bool(value);

		bool DeepEqual<Bool>.deepEqual(Bool b) => value == b.value;
		Dat ToData<Bool>.toDat() => Dat.boolean(value);

		[Instance, AllPure, JsBinary("===")]
		public static Bool _eq(Bool a, Bool b) => of(a.value == b.value);

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.callToString))]
		public static String str(Bool b) => String.of(b.value.ToString());
	}

	[NoImport, JsPrimitive]
	public struct Nat : ToData<Nat> {
		[Hid] internal readonly uint value;
		Nat(uint value) { this.value = value; }

		[Hid] public static Nat of(uint value) => new Nat(value);

		bool DeepEqual<Nat>.deepEqual(Nat n) => value == n.value;
		Dat ToData<Nat>.toDat() => Dat.nat(value);

		[Instance, AllPure, JsBinary("===")]
		public static Bool _eq(Nat a, Nat b) => Bool.of(a.value == b.value);

		[Instance, AllPure, JsBinary("+")]
		public static Nat _add(Nat a, Nat b) => of(checked(a.value + b.value));

		[Instance, AllPure, JsBinary("-")]
		public static Int _sub(Nat a, Nat b) =>
			Int.of(unsignedDiff(a.value, b.value));

		[Instance, AllPure, JsBinary("*")]
		public static Nat _mul(Nat a, Nat b) => Nat.of(checked(a.value * b.value));

		[Instance, AllPure]
		public static Nat _div(Nat a, Nat b) => Nat.of(checked(a.value / b.value));

		[Instance, AllPure]
		public static Nat decr(Nat n) => Nat.of(checked(n.value - 1));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.incr))]
		public static Nat incr(Nat n) => Nat.of(checked(n.value + 1));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.callToString))]
		public static String str(Nat n) => String.of(n.value.ToString());

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Int to_int(Nat n) => Int.of(checked((int)n.value));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Real to_real(Nat n) => Real.of((double)n.value);
	}

	[NoImport, JsPrimitive]
	public struct Int : ToData<Int> {
		[AllPure]
		public static Int parse(String s) =>
			of(int.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly int value;
		Int(int value) { this.value = value; }

		[Hid] public static Int of(int value) => new Int(value);

		bool DeepEqual<Int>.deepEqual(Int i) => value == i.value;
		Dat ToData<Int>.toDat() => Dat.@int(value);

		[Instance, AllPure, JsBinary("===")]
		public static Bool _eq(Int a, Int b) => Bool.of(a.value == b.value);

		[Instance, AllPure, JsBinary("+")]
		public static Int _add(Int a, Int b) => of(checked(a.value + b.value));

		[Instance, AllPure, JsBinary("-")]
		public static Int _sub(Int a, Int b) => of(checked(a.value - b.value));

		[Instance, AllPure, JsBinary("*")]
		public static Int _mul(Int a, Int b) => of(checked(a.value * b.value));

		[Instance, AllPure]
		public static Int _div(Int a, Int b) => of(checked(a.value / b.value));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.callToString))]
		public static String str(Int i) => String.of(i.value.ToString());

		[Instance, AllPure]
		public static Nat to_nat(Int i) => Nat.of(checked((uint)i.value));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.id))]
		public static Real to_real(Int i) => Real.of((double)i.value);
	}

	[NoImport, JsPrimitive]
	public struct Real : ToData<Real> {
		[AllPure]
		public static Real parse(String s) =>
			of(double.Parse(s.value)); //TODO: exceptions

		[Hid] internal readonly double value;

		Real(double value) { this.value = value; }

		[Hid] public static Real of(double d) => new Real(d);

		bool DeepEqual<Real>.deepEqual(Real f) => value == f.value;
		Dat ToData<Real>.toDat() => Dat.realDat(value);

		[Instance, AllPure, JsBinary("+")]
		public static Real _add(Real a, Real b) => of(checked(a.value + b.value));

		[Instance, AllPure, JsBinary("-")]
		public static Real _sub(Real a, Real b) => of(checked(a.value - b.value));

		[Instance, AllPure, JsBinary("*")]
		public static Real _mul(Real a, Real b) => of(checked(a.value * b.value));

		[Instance, AllPure, JsBinary("/")]
		public static Real _div(Real a, Real b) => of(checked(a.value / b.value));

		[Instance, AllPure, JsSpecialTranslate(nameof(JsBuiltins.callToString))]
		public static String str(Real r) => String.of(r.value.ToString());

		[Instance, AllPure]
		public static Int round(Real r) => Int.of(checked((int)Math.Round(r.value)));

		[Instance, AllPure]
		public static Int round_down(Real r) => Int.of(checked((int)Math.Floor(r.value)));

		[Instance, AllPure]
		public static Int round_up(Real r) => Int.of(checked((int)Math.Ceiling(r.value)));
	}

	[NoImport, JsPrimitive]
	public struct String : ToData<String> {
		[Hid] internal readonly string value;

		String(string value) { this.value = value; }

		[Hid] public static String of(string value) => new String(value);

		bool DeepEqual<String>.deepEqual(String s) => value == s.value;
		Dat ToData<String>.toDat() => Dat.str(value);

		[Instance, AllPure, JsBinary("===")]
		public static Bool _eq(String a, String b) => Bool.of(a.value == b.value);

		[Instance, AllPure, JsBinary("+")]
		public static String _add(String a, String b) => of(a.value + b.value);
	}

	[HidSuperClass]
	public abstract class Exception : System.Exception {
		protected Exception() : base() {}
		[AllPure] public abstract String description();
	}

	public sealed class Assertion_Exception : Exception {
		public Assertion_Exception() : base() {}
		public override String description() => String.of("Assertion failed.");
	}

	public interface Console_App {
		//[SelfIo, ReturnPure] List<String> commandLineArguments();
		[SelfIo, ReturnIo] Read_Stream stdin();
		[SelfIo, ReturnIo] Write_Stream stdout();
		[SelfIo, ReturnIo] Write_Stream stderr();

		[SelfIo, ReturnIo] File_System installation_directory();
		[SelfIo, ReturnIo] File_System current_working_directory();
	}

	public interface Read_Stream {
		[SelfIo, ReturnPure] String read_all();
		[SelfIo, ReturnPure] Void write_all_to([Io] Write_Stream w);
		[SelfIo, ReturnPure] Void close();
	}

	public interface Write_Stream {
		[SelfIo, ReturnPure] Void write_all([Pure] String s);
		[SelfIo, ReturnPure] Void write([Pure] String s);
		[SelfIo, ReturnPure] Void write_line([Pure] String s);
		[SelfIo, ReturnPure] Void close();
	}

	public interface File_System {
		[SelfIo, ReturnPure] String read([Pure] Path p);
		[SelfIo, ReturnPure] Void write([Pure] Path p, [Pure] String content);

		[SelfIo, ReturnIo] Read_Stream open_read([Pure] Path p);
		[SelfIo, ReturnIo] Write_Stream open_write([Pure] Path p);
	}

	public struct Path {
		readonly global::Path value;

		Path(global::Path path) { this.value = path; }

		[AllPure] public static Path from_string(String pathString) => new Path(global::Path.fromString(pathString.value));
		[Instance, AllPure] public static String to_string(Path p) => String.of(p.value.toPathString());

		[Instance, AllPure] public static Path directory(Path p) => new Path(p.value.directory());

		[Instance, AllPure] public static Path child(Path p, String childName) => new Path(p.value.child(childName.value));
	}
}
