using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using static Utils;

[DebuggerDisplay(":{str}")]
struct Sym : ToData<Sym>, IEquatable<Sym> {
	readonly int id;
	Sym(int id) { this.id = id; }

	static int nextId = 0;
	static readonly object lockMe = new object();
	static readonly ConcurrentDictionary<string, Sym> stringToSym = new ConcurrentDictionary<string, Sym>();
	static readonly ConcurrentDictionary<Sym, string> symToString = new ConcurrentDictionary<Sym, string>();
	internal static Sym of(string s) {
		if (stringToSym.TryGetValue(s, out var sym))
			return sym;

		lock (lockMe) {
			// I'm the only one here. But someone maybe beat me to it.
			if (stringToSym.TryGetValue(s, out sym))
				// It was replaced.
				return sym;

			sym = new Sym(nextId);
			nextId++;
			var added = stringToSym.TryAdd(s, sym);
			// Should have definitely succeeded because of the lock.
			assert(added);
			var added2 = symToString.TryAdd(sym, s);
			assert(added2);
			return sym;
		}
	}

	internal string str => symToString[this];

	public override string ToString() => str;
	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => id;
	public bool deepEqual(Sym s) => Equals(s);
	public bool Equals(Sym s) => this == s;
	public static bool operator ==(Sym a, Sym b) => a.id == b.id;
	public static bool operator !=(Sym a, Sym b) => a.id != b.id;
	public Dat toDat() => Dat.str(str);
}
