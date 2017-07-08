using System;

namespace Model {
	abstract class ModelElement {
		public override bool Equals(object o) => throw new NotSupportedException();
		public override int GetHashCode() => throw new NotSupportedException();
	}

	// This is always a ClassLike currently. Eventually we'll add instantiated generic classes too.
	abstract class Ty : ModelElement, ToData<Ty>, Identifiable<ClassLike.Id> {
		internal abstract bool isAbstract { get; } //kill
		internal abstract Sym name { get; }
		internal abstract Arr<Super> supers { get; }

		public abstract bool deepEqual(Ty ty);
		public override abstract int GetHashCode();
		public abstract Dat toDat();

		public bool fastEquals(Ty other) => object.ReferenceEquals(this, other);

		public abstract ClassLike.Id getId();
	}

	abstract class ClassLike : Ty, Identifiable<ClassLike.Id> {
		// For a builtin type, identified by the builtin name.
		// For a
		internal struct Id : ToData<Id> {
			// If this is a builtin, this will be missing.
			private readonly string id;
			Id(string id) { this.id = id; }
			internal static Id ofPath(Path path) => new Id(path.toPathString());
			internal static Id ofBuiltin(Sym name) => new Id(name.str);
			public bool deepEqual(Id i) => id == i.id;
			public Dat toDat() => Dat.str(id);
		}

		readonly Sym _name;
		internal override Sym name => _name;
		internal abstract Dict<Sym, Member> membersMap { get; }

		protected ClassLike(Sym name) { _name = name; }
	}

	sealed class Super : ModelElement, ToData<Super>, Identifiable<Super.Id> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Klass containingClass;
		[UpPointer] internal readonly Ty superClass;
		//internal readonly Arr<Impl> impls;
		Late<Arr<Impl>> _impls;
		internal Arr<Impl> impls { get => _impls.get; set => _impls.set(value); }

		internal Super(Loc loc, Klass klass, Ty superClass) {
			this.loc = loc;
			this.containingClass = klass;
			this.superClass = superClass;
		}

		public bool deepEqual(Super s) =>
			containingClass.equalsId<Klass, ClassLike.Id>(s.containingClass) &&
			superClass.equalsId<Ty, ClassLike.Id>(s.superClass) &&
			impls.deepEqual(s.impls);
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(superClass), superClass.getId(), nameof(impls), Dat.arr(impls));
		public Id getId() => new Id(containingClass.getId(), superClass.getId());

		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id classId;
			internal readonly ClassLike.Id superClassId;
			internal Id(ClassLike.Id classId, ClassLike.Id superClassId) {
				this.classId = classId;
				this.superClassId = superClassId;
			}

			public bool deepEqual(Id i) =>
				classId.deepEqual(i.classId) &&
				superClassId.deepEqual(i.superClassId);
			public Dat toDat() => Dat.of(this, nameof(classId), classId, nameof(superClassId), superClassId);
		}
	}

	internal sealed class Impl : ModelElement, ToData<Impl> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Super super;
		[UpPointer] internal readonly Method implemented;
		internal readonly Expr body;

		internal Impl(Super super, Loc loc, Method implemented, Expr body) {
			this.super = super;
			this.loc = loc;
			this.implemented = implemented;
			this.body = body;
		}

		public bool deepEqual(Impl i) =>
			loc.deepEqual(i.loc) &&
			implemented.equalsId<Method, Method.Id>(i.implemented) &&
			body.deepEqual(i.body);
		public Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(super), super.getId(),
			nameof(implemented), implemented.getId(),
			nameof(body), body);
	}

	// Slot or Method
	abstract class Member : ModelElement, ToData<Member> {
		internal readonly Loc loc;
		internal readonly Sym name;
		protected Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
		public abstract bool deepEqual(Member m);
		public abstract Dat toDat();
	}
}
