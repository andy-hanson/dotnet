using static Utils;

namespace Model {
	struct Effect : ToData<Effect> {
		readonly Kind kind;
		Effect(Kind kind) { this.kind = kind; }

		/** True pure function. */
		internal static readonly Effect Pure = new Effect(Kind.Pure);
		/**
		Allowed to observe mutable state.
		Will return the same result if called twice *immediately*, but not if there are intervening state changes.
		*/
		internal static readonly Effect Get = new Effect(Kind.Get);
		/**
		Allowed to alter state in memory.
		Not allowed to change state external to the program.
		*/
		internal static readonly Effect Set = new Effect(Kind.Set);
		/**
		Allowed to interact with the outside world.
		For JavaScript emit, assumed to be async.
		*/
		internal static readonly Effect Io = new Effect(Kind.Io);

		public bool deepEqual(Effect b) => kind == b.kind;
		public Dat toDat() => Dat.str(show);

		/** E.g., one may `get` from a `set` object. */
		internal bool contains(Effect b) => kind >= b.kind;

		internal Effect minCommonEffect(Effect b) =>
			contains(b) ? b : this;

		internal bool isPure => kind == Kind.Pure;
		internal bool canGet => contains(Get);
		internal bool canSet => contains(Set);
		internal bool canIo => contains(Io);

		internal string show {
			get {
				switch (kind) {
					case Kind.Pure: return "pure";
					case Kind.Get: return "get";
					case Kind.Set: return "set";
					case Kind.Io: return "io";
					default: throw unreachable();
				}
			}
		}

		/** Ordering is important; strictest level comes first, each new level contains the previous one. */
		enum Kind { Pure, Get, Set, Io }
	}
}
