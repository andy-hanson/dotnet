namespace Model {
	// Method, Slot, or AbstractMethod
	abstract class Member : ModelElement, ToData<Member>, Identifiable<MemberId> {
		internal readonly Loc loc;
		internal readonly Sym name;
		protected Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }

		internal abstract ClassLike klass { get; }
		public abstract bool deepEqual(Member m);
		public abstract Dat toDat();
		MemberId Identifiable<MemberId>.getId() => getMemberId();
		internal abstract MemberId getMemberId();
		internal abstract string showKind();
	}

	abstract class MemberId : ToData<MemberId> {
		public abstract bool deepEqual(MemberId m);
		public abstract Dat toDat();
	}
}
