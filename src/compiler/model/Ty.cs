namespace Model {
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

		internal void Deconstruct(out Effect effect, out ClsRef cls) {
			effect = this.effect;
			cls = this.cls;
		}

		internal static readonly Ty Void = pure(BuiltinClass.Void);
		internal static readonly Ty Bool = pure(BuiltinClass.Bool);
		internal static readonly Ty Nat = pure(BuiltinClass.Nat);
		internal static readonly Ty Int = pure(BuiltinClass.Int);
		internal static readonly Ty Real = pure(BuiltinClass.Real);
		internal static readonly Ty String = pure(BuiltinClass.String);

		public bool deepEqual(Ty ty) => effect == ty.effect && cls.deepEqual(ty.cls);
		public Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show()), nameof(cls), cls);
		public Id getId() => new Id(effect, cls.getClsRefId());

		internal struct Id : ToData<Id> {
			internal readonly Effect effect;
			internal readonly ClsRefId clsId;
			internal Id(Effect effect, ClsRefId clsId) { this.effect = effect; this.clsId = clsId; }
			public bool deepEqual(Id i) => effect == i.effect && clsId.deepEqual(i.clsId);
			public Dat toDat() => Dat.of(this, nameof(effect), Dat.str(effect.show()), nameof(clsId), clsId);
		}
	}
}
