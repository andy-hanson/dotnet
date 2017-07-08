using System;

namespace Model {
	abstract class Pattern : ModelElement, ToData<Pattern> {
		internal readonly Loc loc;
		Pattern(Loc loc) { this.loc = loc; }

		public abstract bool deepEqual(Pattern p);
		public abstract Dat toDat();

		internal sealed class Ignore : Pattern, ToData<Ignore> {
			internal Ignore(Loc loc) : base(loc) { }
			public override bool deepEqual(Pattern p) => p is Ignore i && deepEqual(i);
			public bool deepEqual(Ignore i) => loc.deepEqual(i.loc);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}
		internal sealed class Single : Pattern, ToData<Single>, Identifiable<Sym>, IEquatable<Single> {
			[NotData] internal readonly Ty ty; // Inferred type of the local.
			internal readonly Sym name;
			internal Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}

			bool IEquatable<Single>.Equals(Single s) => object.ReferenceEquals(this, s);
			public override int GetHashCode() => name.GetHashCode();
			public override bool deepEqual(Pattern p) => p is Single s && deepEqual(s);
			public bool deepEqual(Single s) => loc.deepEqual(s.loc) && name.deepEqual(s.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
			public Sym getId() => name;
		}
		internal sealed class Destruct : Pattern, ToData<Destruct> {
			internal readonly Arr<Pattern> destructuredInto;
			internal Destruct(Loc loc, Arr<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}

			public override bool deepEqual(Pattern p) => p is Destruct d && deepEqual(d);
			public bool deepEqual(Destruct d) => loc.deepEqual(d.loc) && destructuredInto.deepEqual(d.destructuredInto);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(destructuredInto), Dat.arr(destructuredInto));
		}
	}
}
