using System;

namespace Model {
	abstract class ModelElement {
		public override bool Equals(object o) => throw new NotSupportedException();
		public override int GetHashCode() => throw new NotSupportedException();
	}

	struct Ty : ToData<Ty>, Identifiable<Ty.Id> {
		internal readonly Effect effect;
		internal readonly ClsRef cls;
		Ty(Effect effect, ClsRef cls) {
			this.effect = effect;
			this.cls = cls;
		}
		internal static Ty of(Effect effect, ClsRef clsRef) => new Ty(effect, clsRef);
		static Ty pure(ClsRef cls) => of(Effect.Pure, cls);
		internal static Ty io(ClsRef cls) => of(Effect.Io, cls);

		internal static readonly Ty Void = pure(BuiltinClass.Void);
		internal static readonly Ty Bool = pure(BuiltinClass.Bool);
		internal static readonly Ty Nat = pure(BuiltinClass.Nat);
		internal static readonly Ty Int = pure(BuiltinClass.Int);
		internal static readonly Ty Real = pure(BuiltinClass.Real);
		internal static readonly Ty String = pure(BuiltinClass.String);

		public bool deepEqual(Ty ty) => effect == ty.effect && cls.deepEqual(ty.cls);
		public Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show()), nameof(cls), cls);
		public Id getId() => new Id(effect, cls.getId());

		internal struct Id : ToData<Id> {
			internal readonly Effect effect;
			internal readonly ClassLike.Id clsId;
			internal Id(Effect effect, ClassLike.Id clsId) { this.effect = effect; this.clsId = clsId; }
			public bool deepEqual(Id i) => effect == i.effect && clsId.deepEqual(i.clsId);
			public Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show()), nameof(clsId), clsId);
		}
	}

	// This is always a ClassLike currently. Eventually we'll add instantiated generic classes too.
	abstract class ClsRef : ModelElement, ToData<ClsRef>, Identifiable<ClassLike.Id> {
		internal abstract bool isAbstract { get; } //kill
		internal abstract Sym name { get; }
		internal abstract Arr<Super> supers { get; }

		public abstract bool deepEqual(ClsRef ty);
		public override abstract int GetHashCode();
		public abstract Dat toDat();

		public bool fastEquals(ClsRef other) => object.ReferenceEquals(this, other);

		public abstract ClassLike.Id getId();
	}

	abstract class ClassLike : ClsRef, Identifiable<ClassLike.Id> {
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
			superClass.equalsId<ClsRef, ClassLike.Id>(s.superClass) &&
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

	interface MethodOrImplOrExpr {}

	interface MethodOrImpl : MethodOrImplOrExpr {
		Method implementedMethod { get; }
		Klass klass { get; }
	}

	internal sealed class Impl : ModelElement, MethodOrImpl, ToData<Impl> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Super super;
		[UpPointer] internal readonly Method implemented;
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

		internal Impl(Super super, Loc loc, Method implemented) {
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

	// Slot or Method
	abstract class Member : ModelElement, ToData<Member> {
		internal readonly Loc loc;
		internal readonly Sym name;
		protected Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
		public abstract bool deepEqual(Member m);
		public abstract Dat toDat();
	}
}
