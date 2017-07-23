using System;
using System.Diagnostics;

using static Utils;

namespace Model {
	[DebuggerDisplay("Klass {name}")]
	sealed class Klass : ClassLike, ToData<Klass>, IEquatable<Klass> {
		[UpPointer] internal readonly Module module;
		internal readonly Loc loc;

		internal override bool isAbstract => head is KlassHead.Abstract;

		internal Klass(Module module, Loc loc, Sym name) : base(name) {
			this.module = module;
			this.loc = loc;
		}

		public override ClassLike.Id getId() => ClassLike.Id.ofPath(module.logicalPath);

		Late<KlassHead> _head;
		internal KlassHead head { get => _head.get; set => _head.set(value); }

		Late<Arr<Super>> _supers;
		internal override Arr<Super> supers => _supers.get;
		internal void setSupers(Arr<Super> value) => _supers.set(value);

		Late<Arr<MethodWithBody>> _methods;
		internal Arr<MethodWithBody> methods { get => _methods.get; set => _methods.set(value); }

		Late<Dict<Sym, Member>> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap.get;
		internal void setMembersMap(Dict<Sym, Member> value) => _membersMap.set(value);

		public bool Equals(Klass k) => object.ReferenceEquals(this, k);
		public override int GetHashCode() => name.GetHashCode();
		public override bool deepEqual(ClsRef cls) => cls is Klass k && deepEqual(k);
		public bool deepEqual(Klass k) => throw TODO();
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(head), head, nameof(membersMap), Dat.dict(membersMap));
	}

	internal abstract class KlassHead : ModelElement, ToData<KlassHead> {
		KlassHead() {}

		public abstract bool deepEqual(KlassHead head);
		public abstract Dat toDat();

		internal class Static : KlassHead, ToData<Static> {
			internal static readonly Static instance = new Static();
			Static() {}
			public override bool deepEqual(KlassHead h) => object.ReferenceEquals(this, h);
			public bool deepEqual(Static s) => object.ReferenceEquals(this, s);
			public override Dat toDat() => Dat.of(this);
		}

		internal class Abstract : KlassHead, ToData<Abstract> {
			internal readonly Loc loc;
			// These will all be Method.AbstractMethod. But we want a type compatible with BuiltinClass.abstractMethods.
			internal readonly Arr<AbstractMethodLike> abstractMethods;

			internal Abstract(Loc loc, Arr<AbstractMethodLike> abstractMethods) {
				this.loc = loc;
				this.abstractMethods = abstractMethods;
			}
			public override bool deepEqual(KlassHead h) => h is Abstract a && deepEqual(a);
			public bool deepEqual(Abstract a) => loc.deepEqual(a.loc);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		internal class Slots : KlassHead, ToData<Slots>, Identifiable<Klass.Id> {
			internal readonly Loc loc;
			[ParentPointer] internal readonly Klass klass;
			Late<Arr<Slot>> _slots;
			internal Arr<Slot> slots { get => _slots.get; set => _slots.set(value); }

			internal Slots(Loc loc, Klass klass) {
				this.loc = loc;
				this.klass = klass;
			}

			public override bool deepEqual(KlassHead h) => h is Slots s && deepEqual(s);
			public bool deepEqual(Slots s) => slots.deepEqual(s.slots);
			public override Dat toDat() => Dat.of(this, nameof(slots), Dat.arr(slots));
			public Klass.Id getId() => klass.getId();
		}
	}
}
