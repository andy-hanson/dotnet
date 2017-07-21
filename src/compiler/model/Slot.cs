using System;

namespace Model {
	sealed class Slot : Member, ToData<Slot>, Identifiable<Slot.Id>, IEquatable<Slot> {
		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id klass;
			internal readonly Sym name;
			internal Id(ClassLike.Id klass, Sym name) { this.klass = klass; this.name = name; }
			public bool deepEqual(Id i) => klass.deepEqual(i.klass) && name.deepEqual(i.name);
			public Dat toDat() => Dat.of(this, nameof(klass), klass, nameof(name), name);
		}

		[ParentPointer] internal readonly Klass.Head.Slots slots;
		internal readonly bool mutable;
		internal readonly Ty ty;

		internal Slot(Klass.Head.Slots slots, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
			this.slots = slots;
			this.mutable = mutable;
			this.ty = ty;
		}

		bool IEquatable<Slot>.Equals(Slot s) => deepEqual(s);
		public override int GetHashCode() => name.GetHashCode();
		public override bool deepEqual(Member m) => m is Slot s && deepEqual(s);
		public bool deepEqual(Slot s) => loc.deepEqual(s.loc) && name.deepEqual(s.name) && mutable == s.mutable && ty.equalsId<Ty, Ty.Id>(s.ty);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name, nameof(mutable), Dat.boolean(mutable), nameof(ty), ty);
		public Id getId() => new Id(slots.klass.getId(), name);
	}
}
