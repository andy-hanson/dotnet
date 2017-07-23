using System;

namespace Model {
	abstract class ModelElement {
		public override bool Equals(object o) => throw new NotSupportedException();
		public override int GetHashCode() => throw new NotSupportedException();
	}

	// This is always a ClassLike currently. Eventually we'll add instantiated generic classes too.
	abstract class ClsRef : ModelElement, ToData<ClsRef>, Identifiable<ClsRefId> {
		internal abstract bool isAbstract { get; } //kill
		internal abstract Sym name { get; }
		internal abstract Arr<Super> supers { get; }

		public abstract bool deepEqual(ClsRef ty);
		public override abstract int GetHashCode();
		public abstract Dat toDat();

		public bool fastEquals(ClsRef other) => object.ReferenceEquals(this, other);

		ClsRefId Identifiable<ClsRefId>.getId() => getClsRefId();
		public abstract ClsRefId getClsRefId();
	}
	internal struct ClsRefId : ToData<ClsRefId> {
		//Since ClsRef == ClassLike for now...
		readonly ClassLike.Id inner;
		internal ClsRefId(ClassLike.Id inner) { this.inner = inner; }
		public bool deepEqual(ClsRefId i) => inner.deepEqual(i.inner);
		public Dat toDat() => inner.toDat();
	}

	abstract class ClassLike : ClsRef, Identifiable<ClassLike.Id> {
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
		public override ClsRefId getClsRefId() => new ClsRefId(getId());
		public abstract Id getId();

		protected ClassLike(Sym name) { _name = name; }
	}

	sealed class Super : ModelElement, ToData<Super>, Identifiable<Super.Id> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Klass containingClass;
		[UpPointer] internal readonly ClsRef superClass;
		Late<Arr<Impl>> _impls;
		internal Arr<Impl> impls { get => _impls.get; set => _impls.set(value); }

		internal Super(Loc loc, Klass klass, ClsRef superClass) {
			this.loc = loc;
			this.containingClass = klass;
			this.superClass = superClass;
		}

		public bool deepEqual(Super s) =>
			containingClass.equalsId<Klass, ClassLike.Id>(s.containingClass) &&
			superClass.equalsId<ClsRef, ClsRefId>(s.superClass) &&
			impls.deepEqual(s.impls);
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(superClass), superClass.getClsRefId(), nameof(impls), Dat.arr(impls));
		public Id getId() => new Id(containingClass.getId(), superClass.getClsRefId());

		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id classId;
			internal readonly ClsRefId superClassId;
			internal Id(ClassLike.Id classId, ClsRefId superClassId) {
				this.classId = classId;
				this.superClassId = superClassId;
			}

			public bool deepEqual(Id i) =>
				classId.deepEqual(i.classId) &&
				superClassId.deepEqual(i.superClassId);
			public Dat toDat() => Dat.of(this, nameof(classId), classId, nameof(superClassId), superClassId);
		}
	}

	interface MethodOrImplOrExpr {}

	interface MethodOrImpl : MethodOrImplOrExpr {
		Method implementedMethod { get; }
		Klass klass { get; }
	}

	internal sealed class Impl : ModelElement, MethodOrImpl, ToData<Impl> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Super super;
		[UpPointer] internal readonly AbstractMethodLike implemented;
		Late<Expr> _body;
		internal Expr body {
			get => _body.get;
			set {
				value.parent = this;
				_body.set(value);
			}
		}

		Klass MethodOrImpl.klass => super.containingClass;
		Method MethodOrImpl.implementedMethod => implemented;

		internal Impl(Super super, Loc loc, AbstractMethodLike implemented) {
			this.super = super;
			this.loc = loc;
			this.implemented = implemented;
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
}
