using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using static Utils;

[DebuggerDisplay(":{str}")]
struct Sym : IEquatable<Sym> {
	readonly int id;
	Sym(int id) { this.id = id; }

	static int nextId = 0;
	static object lockMe = new object(); //This is stupid
	static ConcurrentDictionary<string, Sym> stringToSym = new ConcurrentDictionary<string, Sym>();
	static ConcurrentDictionary<Sym, string> symToString = new ConcurrentDictionary<Sym, string>();
	internal static Sym of(string s) {
		if (stringToSym.TryGetValue(s, out var sym))
			return sym;

		lock (lockMe) {
			//I'm the only one here. But someone maybe beat me to it.
			if (stringToSym.TryGetValue(s, out sym))
				//It was replaced.
				return sym;

			sym = new Sym(nextId);
			nextId++;
			var added = stringToSym.TryAdd(s, sym);
			//Should have definitely succeeded because of the lock.
			assert(added);
			var added2 = symToString.TryAdd(sym, s);
			assert(added2);
			return sym;
		}
	}

	internal string str {
		get {
			var got = symToString.TryGetValue(this, out var res);
			assert(got);
			return res;
		}
	}

	public override string ToString() => str;
	public override bool Equals(object o) => o is Sym && Equals((Sym)o);
	public override int GetHashCode() => id;
	bool IEquatable<Sym>.Equals(Sym s) => this == s;
	public static bool operator ==(Sym a, Sym b) => a.id == b.id;
	public static bool operator !=(Sym a, Sym b) => a.id != b.id;
}
