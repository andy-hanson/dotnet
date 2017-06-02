using System.Collections.Concurrent;

sealed class Sym {
	private static ConcurrentDictionary<string, Sym> table = new ConcurrentDictionary<string, Sym>();
	public static Sym of(string s) => table.GetOrAdd(s, _ => new Sym(s));

	public readonly string str;
	private Sym(string str) {
		this.str = str;
	}
}
