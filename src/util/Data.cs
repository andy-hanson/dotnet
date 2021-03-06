using System;

using static Utils;

interface DeepEqual<T> where T : DeepEqual<T> {
	bool deepEqual(T other);
}

interface ToData<T> : DeepEqual<T> where T : ToData<T> {
	Dat toDat();
}

[AttributeUsage(AttributeTargets.Field)]
sealed class ParentPointerAttribute : Attribute {} // Like an UpPointer, but should be completely ignored for serialization.
[AttributeUsage(AttributeTargets.Field)]
sealed class UpPointerAttribute : Attribute {}
[AttributeUsage(AttributeTargets.Field)]
sealed class NotDataAttribute : Attribute {}

interface Identifiable<IdType> where IdType : ToData<IdType> {
	/**
	This should be true if two values have the same id.
	They don't necessarily have to have equal content.
	For example, two Module objects with the same path have equalId,
	When an object has an up-pointer, its Equals implementation should call equalsId on its up-pointers to avoid infinite recursion.

	`a.equalsId(b)` should be true iff `a.getId().deepEqual(b.getId())`.
	*/
	IdType getId();
}

static class IdentifiableU {
	/**
	Slow! Use only in test assertions.
	Unlike `fastEquals`, this will be true for corresponding objects in identical trees.
	*/
	public static bool equalsId<T, U>(this T a, T b) where T : Identifiable<U> where U : ToData<U> =>
		a.getId().deepEqual(b.getId());
}

public abstract class Dat : ToData<Dat> {
	Dat() {}

	internal abstract void write(Writer w);

	public Dat toDat() => this;

	public abstract Type type { get; }

	public bool deepEqual(Dat other) => throw new NotSupportedException();

	internal static Dat either<L, R>(Either<L, R> e) where L : ToData<L> where R : ToData<R> =>
		e.isLeft ? e.left.toDat() : e.right.toDat();

	internal static Dat op<T>(Op<T> op) where T : ToData<T> => new OpDat<T>(op);
	internal static Dat op(Op<string> op) => new OpDat<Dat>(op.get(out var s) ? Op.Some(str(s)) : Op<Dat>.None);
	internal static Dat op(OpUint op) => new OpDat<Dat>(op.map(nat));
	internal static Dat boolean(bool b) => new BoolDat(b);
	internal static Dat @int(int i) => new IntDat(i);
	internal static Dat nat(uint u) => new UintDat(u);
	internal static Dat realDat(double f) => new RealDat(f);
	internal static Dat str(string s) => new StrDat(s);
	internal static Dat arr<T>(Arr<T> a) where T : ToData<T> => new ArrDat<T>(a);
	internal static Dat arr<T, U>(Arr<Either<T, U>> a) where T : ToData<T> where U : ToData<U> =>
		new ArrDat<Dat>(a.map(em => em.toDat()));
	internal static Dat arrOfIds<T, U>(Arr<T> a) where T : Identifiable<U> where U : ToData<U> => new ArrDat<U>(a.map(em => em.getId()));
	internal static Dat arr(Arr<char> a) => new ArrDat<Dat>(a.map(ch => str(ch.ToString())));
	internal static Dat arr(Arr<string> a) => new ArrDat<Dat>(a.map(str));
	internal static Dat dict(Dict<string, Arr<string>> d) => new DictDat<Dat>(d.mapValues<Dat>(v => new ArrDat<Dat>(v.map(str))));
	internal static Dat dict(Dict<Sym, Arr<Sym>> d) => new DictDat<Dat>(d.map<string, Dat>((k, v) => (k.str, new ArrDat<Sym>(v))));
	internal static Dat dict<T>(Dict<Sym, T> d) where T : ToData<T> => new DictDat<T>(d.mapKeys(k => k.str));
	internal static Dat dict<T>(Dict<string, T> d) where T : ToData<T> => new DictDat<T>(d);

	class OpDat<T> : Dat where T : ToData<T> {
		readonly Op<T> value;
		internal OpDat(Op<T> value) { this.value = value; }
		public override Type type => typeof(Op<T>);
		internal override void write(Writer w) {
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
		internal override void write(Writer w) => w.writeBool(value);
	}

	internal class IntDat : Dat {
		readonly int value;
		internal IntDat(int value) { this.value = value; }
		public override Type type => typeof(int);
		internal override void write(Writer w) => w.writeInt(value);
	}

	internal class UintDat : Dat {
		readonly uint value;
		internal UintDat(uint value) { this.value = value; }
		public override Type type => typeof(uint);
		internal override void write(Writer w) => w.writeUint(value);
	}

	internal class RealDat : Dat {
		readonly double value;
		internal RealDat(double value) { this.value = value; }
		public override Type type => typeof(float);
		internal override void write(Writer w) => w.writeReal(value);
	}

	internal class StrDat : Dat {
		readonly string value;
		internal StrDat(string value) { this.value = value; }
		public override Type type => typeof(string);
		internal override void write(Writer w) => w.writeQuotedString(value);
	}

	internal class ArrDat<T> : Dat where T : ToData<T> {
		readonly Arr<T> value;
		internal ArrDat(Arr<T> value) { this.value = value; }
		public override Type type => typeof(Arr<T>);
		internal override void write(Writer w) => w.writeArray(value);
	}

	internal class DictDat<T> : Dat where T : ToData<T> {
		readonly Dict<string, T> value;
		internal DictDat(Dict<string, T> value) { this.value = value; }
		public override Type type => typeof(Dict<string, T>);
		internal override void write(Writer w) => w.writeDict(value);
	}

	internal static Dat of<T>(T o) where T : ToData<T> {
		unused(o);
		return new Dat0<T>();
	}

	internal static Dat of<T, V1>(T o, string key1, V1 value1) where T : ToData<T> where V1 : ToData<V1> {
		unused(o);
		return new Dat1<T, V1>(key1, value1);
	}

	internal static Dat of<T, V1, V2>(T o, string key1, V1 value1, string key2, V2 value2) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> {
		unused(o);
		return new Dat2<T, V1, V2>(key1, value1, key2, value2);
	}

	internal static Dat of<T, V1, V2, V3>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> {
		unused(o);
		return new Dat3<T, V1, V2, V3>(key1, value1, key2, value2, key3, value3);
	}

	internal static Dat of<T, V1, V2, V3, V4>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> {
		unused(o);
		return new Dat4<T, V1, V2, V3, V4>(key1, value1, key2, value2, key3, value3, key4, value4);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> {
		unused(o);
		return new Dat5<T, V1, V2, V3, V4, V5>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5, V6>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> {
		unused(o);
		return new Dat6<T, V1, V2, V3, V4, V5, V6>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5, key6, value6);
	}

	internal static Dat of<T, V1, V2, V3, V4, V5, V6, V7>(
		T o, string key1, V1 value1, string key2, V2 value2, string key3, V3 value3, string key4, V4 value4, string key5, V5 value5, string key6, V6 value6, string key7, V7 value7)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> where V7 : ToData<V7> {
		unused(o);
		return new Dat6<T, V1, V2, V3, V4, V5, V6, V7>(key1, value1, key2, value2, key3, value3, key4, value4, key5, value5, key6, value6, key7, value7);
	}

	internal sealed class Dat0<T> : Dat where T : ToData<T> {
		internal Dat0() {}

		public override Type type => typeof(T);
		internal override void write(Writer w) => w.writeDict(this);
	}

	internal sealed class Dat1<T, V1> : Dat where T : ToData<T> where V1 : ToData<V1> {
		internal readonly string key1;
		internal readonly V1 value1;
		internal Dat1(string key1, V1 value1) { this.key1 = key1; this.value1 = value1; }

		public override Type type => typeof(T);
		internal override void write(Writer w) => w.writeDict(this);
	}

	internal sealed class Dat2<T, V1, V2> : Dat where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> {
		internal readonly string key1; internal readonly V1 value1;
		internal readonly string key2; internal readonly V2 value2;
		internal Dat2(string key1, V1 value1, string key2, V2 value2) {
			this.key1 = key1; this.value1 = value1;
			this.key2 = key2; this.value2 = value2;
		}

		public override Type type => typeof(T);
		internal override void write(Writer w) => w.writeDict(this);
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
		internal override void write(Writer w) => w.writeDict(this);
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
		internal override void write(Writer w) => w.writeDict(this);
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
		internal override void write(Writer w) => w.writeDict(this);
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
		internal override void write(Writer w) => w.writeDict(this);
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
		internal override void write(Writer w) => w.writeDict(this);
	}
}
