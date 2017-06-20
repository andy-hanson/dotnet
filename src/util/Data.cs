using System;
using System.Reflection;

using static Utils;

interface DeepEqual<T> where T : DeepEqual<T> {
	bool deepEqual(T other);
}

interface ToData<T> : DeepEqual<T> where T : ToData<T> {
	Dat toDat();
}

[AttributeUsage(AttributeTargets.Field)]
sealed class ParentPointer : Attribute {} // Like an UpPointer, but should be completely ignored for serialization.
[AttributeUsage(AttributeTargets.Field)]
sealed class UpPointer : Attribute {}
[AttributeUsage(AttributeTargets.Field)]
sealed class NotData : Attribute {}

interface Identifiable<IdType> where IdType : ToData<IdType> {
	/**
	This should be true if two values have the same id.
	They don't necessarily have to have equal content.
	For example, two Module objects with the same path have equalId,
	When an object has an up-pointer, its Equals implementation should call equalsId on its up-pointers to avoid infinite recursion.

	`a.equalsId(b)` should be true iff `a.getId().Equals(b.getId())`.
	*/
	IdType getId();
}

static class IdentifiableU {
	/**
	True if two values are actually the same object.
	*/
	//public static bool fastEquals<T, IdType>(T a, T b) where T : class, Identifiable<IdType> where IdType : ToData<IdType> =>
	//	a == b;

	/**
	Slow! Use only in test assertions.
	Unlike `fastEquals`, this will be true for corresponding objects in identical trees.
	*/
	public static bool equalsId<T, U>(this T a, T b) where T : Identifiable<U> where U : ToData<U> =>
		a.getId().Equals(b.getId());
}

abstract class Dat : ToData<Dat> {
	Dat() {}

	public abstract void write(Writer w);

	public Dat toDat() => this;

	public abstract Type type { get; }

	public bool deepEqual(Dat other) => throw new NotImplementedException();

	internal static Dat either<L, R>(Either<L, R> e) where L : ToData<L> where R : ToData<R> =>
		e.isLeft ? e.left.toDat() : e.right.toDat();

	internal static Dat op<T>(Op<T> op) where T : ToData<T> => new OpDat<T>(op);
	internal static Dat op(Op<string> op) => new OpDat<Dat>(op.map(str));
	internal static Dat op(OpUint op) => new OpDat<Dat>(op.map(num));
	internal static Dat boolean(bool b) => new BoolDat(b);
	internal static Dat inum(int i) => new IntDat(i);
	internal static Dat num(uint u) => new UintDat(u);
	internal static Dat floatDat(double f) => new FloatDat(f);
	internal static Dat str(string s) => new StrDat(s);
	internal static Dat arr<T>(Arr<T> a) where T : ToData<T> => new ArrDat<T>(a);
	internal static Dat arr(Arr<char> a) => new ArrDat<Dat>(a.map(ch => str(ch.ToString())));
	internal static Dat arr(Arr<string> a) => new ArrDat<Dat>(a.map(str));
	internal static Dat dict<T>(Dict<Sym, T> d) where T : ToData<T> => new DictDat<T>(d.mapKeys(k => k.str));
	internal static Dat dict<T>(Dict<string, T> d) where T : ToData<T> => new DictDat<T>(d);

	class OpDat<T> : Dat where T : ToData<T> {
		readonly Op<T> value;
		internal OpDat(Op<T> value) { this.value = value; }
		public override Type type => typeof(Op<T>);
		public override void write(Writer w) {
			if (value.get(out var o))
				o.toDat().write(w);
			else
				w.writeNull();
		}
	}

	internal class BoolDat : Dat {
		readonly bool value;
		internal BoolDat(bool value) { this.value = value; }
		public override Type type => typeof(bool);
		public override void write(Writer w) => w.writeBool(value);
	}

	internal class IntDat : Dat {
		readonly int value;
		internal IntDat(int value) { this.value = value; }
		public override Type type => typeof(int);
		public override void write(Writer w) => w.writeInt(value);
	}

	internal class UintDat : Dat {
		readonly uint value;
		internal UintDat(uint value) { this.value = value; }
		public override Type type => typeof(uint);
		public override void write(Writer w) => w.writeUint(value);
	}

	internal class FloatDat : Dat {
		readonly double value;
		internal FloatDat(double value) { this.value = value; }
		public override Type type => typeof(float);
		public override void write(Writer w) => w.writeFloat(value);
	}

	internal class StrDat : Dat {
		readonly string value;
		internal StrDat(string value) { this.value = value; }
		public override Type type => typeof(string);
		public override void write(Writer w) => w.writeQuotedString(value);
	}

	internal class ArrDat<T> : Dat where T : ToData<T> {
		readonly Arr<T> value;
		internal ArrDat(Arr<T> value) { this.value = value; }
		public override Type type => typeof(Arr<T>);
		public override void write(Writer w) => w.writeArray(value);
	}

	internal class DictDat<T> : Dat where T : ToData<T> {
		readonly Dict<string, T> value;
		internal DictDat(Dict<string, T> value) { this.value = value; }
		public override Type type => typeof(Dict<string, T>);
		public override void write(Writer w) => w.writeDict(value);
	}

	internal static Dat of<T>(T o) where T : ToData<T> => new Dat0<T>();

	internal static Dat of<T, V1>(T o, string key1, V1 value1) where T : ToData<T> where V1 : ToData<V1> {
		#if false
			var type = typeof(T);
			assert(o.GetType() == type);
			var fields = getFields<T>();
			assert(fields.Length == 1);
			checkField(fields[0], o, key1, value1);
		#endif
		return new Dat1<T, V1>(key1, value1);
	}

	internal static Dat of<T, V1, V2>(T o, string key1, V1 value1, string key2, V2 value2) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> {
		#if false
			assert(o.GetType() == typeof(T));
			var fields = getFields<T>();
			assert(fields.Length == 2);
			checkField(fields[0], o, key1, value1);
			checkField(fields[1], o, key2, value2);
		#endif
		return new Dat2<T, V1, V2>(key1, value1, key2, value2);
	}

	internal static Dat of<T, V1, V2, V3>(T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> {
		#if false
			assert(o.GetType() == typeof(T));
			var fields = getFields<T>();
			assert(fields.Length == 3);
			checkField(fields[0], o, key1, value1);
			checkField(fields[1], o, key2, value2);
			checkField(fields[2], o, key3, value3);
		#endif
		return new Dat3<T, V1, V2, V3>(key1, value1, key2, value2, key3, value3);
	}

	internal static Dat of<T, V1, V2, V3, V4>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> {
		#if false
			assert(o.GetType() == typeof(T));
			var fields = getFields<T>();
			assert(fields.Length == 4);
			checkField(fields[0], o, key1, value1);
			checkField(fields[1], o, key2, value2);
			checkField(fields[2], o, key3, value3);
			checkField(fields[3], o, key4, value4);
		#endif
		return new Dat4<T, V1, V2, V3, V4>(key1, value1, key2, value2, key3, value3, key4, value4);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> {
		#if false
			assert(o.GetType() == typeof(T));
			var fields = getFields<T>();
			assert(fields.Length == 5);
			checkField(fields[0], o, key1, value1);
			checkField(fields[1], o, key2, value2);
			checkField(fields[2], o, key3, value3);
			checkField(fields[3], o, key4, value4);
			checkField(fields[4], o, key5, value5);
		#endif
		return new Dat5<T, V1, V2, V3, V4, V5>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5, V6>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> {
		return new Dat6<T, V1, V2, V3, V4, V5, V6>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5, key6, value6);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5, V6, V7>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6, string key7, V7 value7)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> where V7 : ToData<V7> {
		return new Dat6<T, V1, V2, V3, V4, V5, V6, V7>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5, key6, value6, key7, value7);
	}

	static void checkField(FieldInfo f, object o, string key, object value) {
		assert(f.Name == key);
		assert(f.GetValue(o) == value);
	}
	static FieldInfo[] getFields<T>() { return typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy); }

	internal sealed class Dat0<T> : Dat where T : ToData<T> {
		internal Dat0() {}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat1<T, V1> : Dat where T : ToData<T> where V1 : ToData<V1> {
		internal readonly string key1;
		internal readonly V1 value1;
		internal Dat1(string key1, V1 value1) { this.key1 = key1; this.value1 = value1; }

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat2<T, V1, V2> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal Dat2(string key1, V1 value1, string key2, V2 value2) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat3<T, V1, V2, V3> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal readonly string key3; internal readonly V3 value3;
		internal Dat3(string key1, V1 value1, string key2, V2 value2, string key3, V3 value3) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
			this.key3 = key3; this.value3 = value3;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat4<T, V1, V2, V3, V4> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal readonly string key3; internal readonly V3 value3;
		internal readonly string key4; internal readonly V4 value4;
		internal Dat4(string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
			this.key3 = key3; this.value3 = value3;
			this.key4 = key4; this.value4 = value4;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat5<T, V1, V2, V3, V4, V5> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal readonly string key3; internal readonly V3 value3;
		internal readonly string key4; internal readonly V4 value4;
		internal readonly string key5; internal readonly V5 value5;
		internal Dat5(string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
			this.key3 = key3; this.value3 = value3;
			this.key4 = key4; this.value4 = value4;
			this.key5 = key5; this.value5 = value5;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat6<T, V1, V2, V3, V4, V5, V6> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal readonly string key3; internal readonly V3 value3;
		internal readonly string key4; internal readonly V4 value4;
		internal readonly string key5; internal readonly V5 value5;
		internal readonly string key6; internal readonly V6 value6;
		internal Dat6(string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
			this.key3 = key3; this.value3 = value3;
			this.key4 = key4; this.value4 = value4;
			this.key5 = key5; this.value5 = value5;
			this.key6 = key6; this.value6 = value6;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
	internal sealed class Dat6<T, V1, V2, V3, V4, V5, V6, V7> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> where V7 : ToData<V7> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal readonly string key3; internal readonly V3 value3;
		internal readonly string key4; internal readonly V4 value4;
		internal readonly string key5; internal readonly V5 value5;
		internal readonly string key6; internal readonly V6 value6;
		internal readonly string key7; internal readonly V7 value7;
		internal Dat6(string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6, string key7, V7 value7) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
			this.key3 = key3; this.value3 = value3;
			this.key4 = key4; this.value4 = value4;
			this.key5 = key5; this.value5 = value5;
			this.key6 = key6; this.value6 = value6;
			this.key7 = key7; this.value7 = value7;
		}

		public override Type type => typeof(T);
		public override void write(Writer w) => w.writeDict(this);
	}
}
