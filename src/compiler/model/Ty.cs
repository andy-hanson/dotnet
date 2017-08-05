using System;

using static Utils;

//mv
interface FastEquals<T> {
	bool fastEquals(T other);
}

namespace Model {
	abstract class Ty : ToData<Ty>, Identifiable<TyId>, FastEquals<Ty> {
		internal static PlainTy of(Effect effect, InstCls instantiatedClass) => new PlainTy(effect, instantiatedClass);
		static PlainTy pure(ClassDeclarationLike cls) => of(Effect.pure, InstCls.of(cls, Arr.empty<Ty>()));
		internal static PlainTy io(InstCls cls) => of(Effect.io, cls);

		internal static readonly Ty bogus = BogusTy.instance;

		public abstract bool fastEquals(Ty ty);
		public abstract bool deepEqual(Ty ty);
		public abstract Dat toDat();
		TyId Identifiable<TyId>.getId() => getTyId();
		public abstract TyId getTyId();

		internal static readonly Ty Void = pure(BuiltinClass.Void);
		internal static readonly Ty Bool = pure(BuiltinClass.Bool);
		internal static readonly Ty Nat = pure(BuiltinClass.Nat);
		internal static readonly Ty Int = pure(BuiltinClass.Int);
		internal static readonly Ty Real = pure(BuiltinClass.Real);
		internal static readonly Ty String = pure(BuiltinClass.String);
	}

	abstract class TyId : ToData<TyId> {
		public abstract bool deepEqual(TyId id);
		public abstract Dat toDat();
	}

	internal sealed class PlainTy : Ty, ToData<PlainTy>, Identifiable<PlainTy.Id> {
		internal readonly Effect effect;
		internal readonly InstCls instantiatedClass;

		internal PlainTy(Effect effect, InstCls instantiatedClass) {
			this.effect = effect;
			this.instantiatedClass = instantiatedClass;
		}
		internal void Deconstruct(out Effect effect, out InstCls instantiatedClass) {
			effect = this.effect;
			instantiatedClass = this.instantiatedClass;
		}

		public override bool fastEquals(Ty ty) => ty is PlainTy p && fastEquals(p);
		bool fastEquals(PlainTy p) =>
			effect.deepEqual(p.effect) &&
			instantiatedClass.fastEquals(p.instantiatedClass);
		public override bool deepEqual(Ty ty) => ty is PlainTy && deepEqual(ty);
		public bool deepEqual(PlainTy p) =>
			effect.deepEqual(p.effect) &&
			instantiatedClass.deepEqual(p.instantiatedClass);
		public override Dat toDat() => Dat.of(this, nameof(effect), effect, nameof(instantiatedClass), instantiatedClass);
		public override TyId getTyId() => getId();
		public Id getId() => new Id(effect, instantiatedClass.getId());

		internal sealed class Id : TyId, ToData<Id> {
			internal readonly Effect effect;
			internal readonly InstCls.Id clsId;
			internal Id(Effect effect, InstCls.Id clsId) { this.effect = effect; this.clsId = clsId; }
			public override bool deepEqual(TyId t) => t is Id i && deepEqual(i);
			public bool deepEqual(Id i) => effect.deepEqual(i.effect) && clsId.deepEqual(i.clsId);
			public override Dat toDat() => Dat.of(this, nameof(effect), effect, nameof(clsId), clsId);
		}
	}

	struct InstCls : ToData<InstCls>, Identifiable<InstCls.Id>, FastEquals<InstCls> { //TODO: rename to InstantiatedClass
		internal readonly ClassDeclarationLike classDeclaration;
		internal readonly Arr<Ty> typeArguments;
		InstCls(ClassDeclarationLike classDeclaration, Arr<Ty> typeArguments) {
			this.classDeclaration = classDeclaration;
			this.typeArguments = typeArguments;
			assert(classDeclaration.typeParameters.length == typeArguments.length);
		}
		internal void Deconstruct(out ClassDeclarationLike classDeclaration, out Arr<Ty> typeArguments) {
			classDeclaration = this.classDeclaration;
			typeArguments = this.typeArguments;
		}
		internal static InstCls of(ClassDeclarationLike classDeclaration, Arr<Ty> typeArguments) => new InstCls(classDeclaration, typeArguments);

		public bool fastEquals(InstCls i) => classDeclaration.fastEquals(i.classDeclaration) && typeArguments.fastEquals(i.typeArguments);
		public bool deepEqual(InstCls i) => classDeclaration.deepEqual(i.classDeclaration) && typeArguments.deepEqual(i.typeArguments);
		public Dat toDat() => Dat.of(this, nameof(classDeclaration), classDeclaration, nameof(typeArguments), Dat.arr(typeArguments));
		public Id getId() => new Id(classDeclaration.getId(), typeArguments.map(t => t.getTyId()));

		internal struct Id : ToData<Id> {
			internal readonly ClassDeclaration.Id classDeclarationId;
			internal readonly Arr<TyId> typeArgumentIds;
			internal Id(ClassDeclaration.Id classDeclarationId, Arr<TyId> typeArgumentIds) {
				this.classDeclarationId = classDeclarationId;
				this.typeArgumentIds = typeArgumentIds;
			}

			public bool deepEqual(Id i) => classDeclarationId.deepEqual(i.classDeclarationId) && typeArgumentIds.deepEqual(i.typeArgumentIds);
			public Dat toDat() => Dat.of(this, nameof(classDeclarationId), classDeclarationId, nameof(typeArgumentIds), Dat.arr(typeArgumentIds));
		}
	}

	// ClassDeclarationLike or MethodDeclaration.
	// Should implement GetHashCode().
	internal interface TypeParameterOrigin : ToData<TypeParameterOrigin>, Identifiable<TypeParameterOriginId> {}

	internal abstract class TypeParameterOriginId : ToData<TypeParameterOriginId> {
		public abstract bool deepEqual(TypeParameterOriginId i);
		public abstract Dat toDat();
	}

	internal sealed class TypeParameter : Ty, ToData<TypeParameter>, IEquatable<TypeParameter> {
		[UpPointer] Late<TypeParameterOrigin> _origin;
		internal TypeParameterOrigin origin { get => _origin.get; set => _origin.set(value); }
		internal readonly Sym name;
		TypeParameter(Sym name) {
			this.name = name;
		}
		internal static TypeParameter create(Sym name) => new TypeParameter(name);

		// It's likely that many type parameters share the same name, so use the origin for the hash code.
		public override int GetHashCode() => origin.GetHashCode();
		public override bool Equals(object o) => throw new NotSupportedException();
		public bool Equals(TypeParameter t) => object.ReferenceEquals(this, t);
		public override bool fastEquals(Ty t) => object.ReferenceEquals(this, t);
		public override bool deepEqual(Ty t) => t is TypeParameter tp && deepEqual(tp);
		public bool deepEqual(TypeParameter tp) =>
			origin.equalsId<TypeParameterOrigin, TypeParameterOriginId>(tp.origin) &&
			name.deepEqual(tp.name);
		public override Dat toDat() => Dat.of(this,
			nameof(origin), origin.getId(),
			nameof(name), name);
		public override TyId getTyId() => new Id(this);

		internal sealed class Id : TyId {
			//just use itself
			internal TypeParameter tp;
			internal Id(TypeParameter tp) { this.tp = tp; }
			public override bool deepEqual(TyId t) => t is Id i && tp.deepEqual(i.tp);
			public override Dat toDat() => tp.toDat();
		}
	}

	internal sealed class BogusTy : Ty {
		internal static readonly BogusTy instance = new BogusTy();
		BogusTy() {}

		public override bool fastEquals(Ty t) => object.ReferenceEquals(this, t);
		public override bool deepEqual(Ty t) => object.ReferenceEquals(this, t);
		public override Dat toDat() => Dat.str(nameof(Bogus));
		public override TyId getTyId() => Id.instance;

		internal sealed class Id : TyId {
			#pragma warning disable S3218 // Allow shadow
			internal static readonly Id instance = new Id();
			#pragma warning disable
			Id() {}
			public override bool deepEqual(TyId t) => object.ReferenceEquals(this, t);
			public override Dat toDat() => Dat.str("Bogus");
		}
	}
}
