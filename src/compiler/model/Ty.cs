namespace Model {
	abstract class Ty : ToData<Ty>, Identifiable<TyId> {
		internal static PlainTy of(Effect effect, ClsRef clsRef) => new PlainTy(effect, clsRef);
		static PlainTy pure(ClsRef cls) => of(Effect.Pure, cls);
		internal static PlainTy io(ClsRef cls) => of(Effect.Io, cls);

		internal static readonly Ty bogus = Bogus.instance;

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

		internal abstract Effect effect { get; }

		internal sealed class PlainTy : Ty, ToData<PlainTy>, Identifiable<PlainTy.Id> {
			readonly Effect _effect;
			internal override Effect effect => _effect;
			internal readonly ClsRef cls;
			internal PlainTy(Effect effect, ClsRef cls) { this._effect = effect; this.cls = cls; }
			internal void Deconstruct(out Effect effect, out ClsRef cls) { effect = this.effect; cls = this.cls; }

			public override bool deepEqual(Ty ty) => ty is PlainTy && deepEqual(ty);
			public bool deepEqual(PlainTy ty) => effect.deepEqual(ty.effect) && cls.deepEqual(ty.cls);
			public override Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show), nameof(cls), cls);
			public override TyId getTyId() => getId();
			public Id getId() => new Id(effect, cls.getClsRefId());

			internal sealed class Id : TyId, ToData<Id> {
				internal readonly Effect effect;
				internal readonly ClsRefId clsId;
				internal Id(Effect effect, ClsRefId clsId) { this.effect = effect; this.clsId = clsId; }
				public override bool deepEqual(TyId t) => t is Id i && deepEqual(i);
				public bool deepEqual(Id i) => effect.deepEqual(i.effect) && clsId.deepEqual(i.clsId);
				public override Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show), nameof(clsId), clsId);
			}
		}

		internal sealed class Bogus : Ty {
			internal static readonly Bogus instance = new Bogus();
			Bogus() {}

			internal override Effect effect => Effect.Pure;

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

		//TODO: other types of types: instantiated generic, type parameter
	}

	abstract class TyId : ToData<TyId> {
		public abstract bool deepEqual(TyId id);
		public abstract Dat toDat();
	}
}
