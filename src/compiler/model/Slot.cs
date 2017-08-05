using System;

namespace Model {
	sealed class SlotDeclaration : MemberDeclaration, ToData<SlotDeclaration>, Identifiable<SlotDeclaration.Id>, IEquatable<SlotDeclaration> {
		[ParentPointer] internal readonly ClassHead.Slots slots;
		internal readonly bool mutable;
		internal readonly Ty ty;

		internal SlotDeclaration(ClassHead.Slots slots, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
			this.slots = slots;
			this.mutable = mutable;
			this.ty = ty;
		}

		internal override ClassDeclarationLike klass => slots.klass;

		bool IEquatable<SlotDeclaration>.Equals(SlotDeclaration s) => deepEqual(s);
		public override int GetHashCode() => name.GetHashCode();
		public override bool deepEqual(MemberDeclaration m) => m is SlotDeclaration s && deepEqual(s);
		public bool deepEqual(SlotDeclaration s) => loc.deepEqual(s.loc) && name.deepEqual(s.name) && mutable == s.mutable && ty.equalsId<Ty, TyId>(s.ty);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name, nameof(mutable), Dat.boolean(mutable), nameof(ty), ty);
		internal override MemberId getMemberId() => getId();
		internal override string showKind(bool upper) => upper ? nameof(SlotDeclaration) : "slot";
		public Id getId() => new Id(slots.klass.getId(), name);

		internal class Id : MemberId, ToData<Id> {
			internal readonly ClassDeclarationLike.Id klass;
			internal readonly Sym name;
			internal Id(ClassDeclarationLike.Id klass, Sym name) { this.klass = klass; this.name = name; }
			public override bool deepEqual(MemberId m) => m is Id i && deepEqual(i);
			public bool deepEqual(Id i) => klass.deepEqual(i.klass) && name.deepEqual(i.name);
			public override Dat toDat() => Dat.of(this, nameof(klass), klass, nameof(name), name);
		}
	}
}
