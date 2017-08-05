using System;
using System.Diagnostics;

using static Utils;

namespace Model {
	[DebuggerDisplay("ClassDeclaration {name}")]
	sealed class ClassDeclaration : ClassDeclarationLike, ToData<ClassDeclaration>, IEquatable<ClassDeclaration> {
		[UpPointer] internal readonly Module module;
		internal readonly Loc loc;

		internal override bool isAbstract => head is ClassHead.Abstract;

		internal ClassDeclaration(Module module, Loc loc, Sym name, Arr<TypeParameter> typeParameters) : base(name, typeParameters) {
			this.module = module;
			this.loc = loc;
		}

		public override ClassDeclarationLike.Id getId() => ClassDeclarationLike.Id.ofPath(module.logicalPath);

		Late<ClassHead> _head;
		internal ClassHead head { get => _head.get; set => _head.set(value); }

		Late<Arr<Super>> _supers;
		internal override Arr<Super> supers => _supers.get;
		internal void setSupers(Arr<Super> value) => _supers.set(value);

		Late<Arr<MethodWithBody>> _methods;
		internal Arr<MethodWithBody> methods { get => _methods.get; set => _methods.set(value); }

		Late<Dict<Sym, MemberDeclaration>> _membersMap;
		internal override Dict<Sym, MemberDeclaration> membersMap => _membersMap.get;
		internal void setMembersMap(Dict<Sym, MemberDeclaration> value) => _membersMap.set(value);

		public bool Equals(ClassDeclaration k) => object.ReferenceEquals(this, k);
		public override int GetHashCode() => name.GetHashCode();
		public override bool deepEqual(ClassDeclarationLike cls) => cls is ClassDeclaration k && deepEqual(k);
		public bool deepEqual(ClassDeclaration k) => throw TODO();
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(head), head, nameof(membersMap), Dat.dict(membersMap));
	}

	internal abstract class ClassHead : ModelElement, ToData<ClassHead> {
		ClassHead() {}

		public abstract bool deepEqual(ClassHead head);
		public abstract Dat toDat();

		internal class Static : ClassHead, ToData<Static> {
			internal static readonly Static instance = new Static();
			Static() {}
			public override bool deepEqual(ClassHead h) => object.ReferenceEquals(this, h);
			public bool deepEqual(Static s) => object.ReferenceEquals(this, s);
			public override Dat toDat() => Dat.of(this);
		}

		internal class Abstract : ClassHead, ToData<Abstract> {
			internal readonly Loc loc;
			// These will all be Method.AbstractMethod. But we want a type compatible with BuiltinClass.abstractMethods.
			internal readonly Arr<AbstractMethodLike> abstractMethods;

			internal Abstract(Loc loc, Arr<AbstractMethodLike> abstractMethods) {
				this.loc = loc;
				this.abstractMethods = abstractMethods;
			}
			public override bool deepEqual(ClassHead h) => h is Abstract a && deepEqual(a);
			public bool deepEqual(Abstract a) => loc.deepEqual(a.loc);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		internal class Slots : ClassHead, ToData<Slots>, Identifiable<ClassDeclaration.Id> {
			internal readonly Loc loc;
			[ParentPointer] internal readonly ClassDeclaration klass;
			Late<Arr<SlotDeclaration>> _slots;
			internal Arr<SlotDeclaration> slots { get => _slots.get; set => _slots.set(value); }

			internal Slots(Loc loc, ClassDeclaration klass) {
				this.loc = loc;
				this.klass = klass;
			}

			public override bool deepEqual(ClassHead h) => h is Slots s && deepEqual(s);
			public bool deepEqual(Slots s) => slots.deepEqual(s.slots);
			public override Dat toDat() => Dat.of(this, nameof(slots), Dat.arr(slots));
			public ClassDeclaration.Id getId() => klass.getId();
		}
	}
}
