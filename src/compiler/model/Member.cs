namespace Model {
	// Method, Slot, or AbstractMethod
	abstract class MemberDeclaration : ModelElement, ToData<MemberDeclaration>, Identifiable<MemberId> {
		internal readonly Loc loc;
		internal readonly Sym name;
		protected MemberDeclaration(Loc loc, Sym name) { this.loc = loc; this.name = name; }

		internal abstract ClassDeclarationLike klass { get; }
		public abstract bool deepEqual(MemberDeclaration m);
		public abstract Dat toDat();
		MemberId Identifiable<MemberId>.getId() => getMemberId();
		internal abstract MemberId getMemberId();
		internal abstract string showKind(bool upper);
	}

	abstract class MemberId : ToData<MemberId> {
		public abstract bool deepEqual(MemberId m);
		public abstract Dat toDat();
	}
}
