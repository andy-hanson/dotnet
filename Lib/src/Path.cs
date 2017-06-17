using System;
using static Utils;

struct Path : ToData<Path> {
	Arr<Sym> parts;

	internal Path(Arr<Sym> parts) {
		this.parts = parts;
	}

	public static Path empty = new Path(Arr.empty<Sym>());

	public static Path resolveWithRoot(Path root, Path path) =>
		new Path(root.parts.Concat(path.parts));

	internal static Path from(params string[] elements) =>
		new Path(elements.map(Sym.of));

	internal static Path fromString(string str) =>
		new Path(str.Split('/').map(Sym.of));

	internal RelPath asRel => new RelPath(0, this);

	internal Path resolve(RelPath rel) {
		var nPartsToKeep = parts.length - rel.nParents;
		if (nPartsToKeep < 0)
			throw new Exception($"Can't resolve: {rel}\nRelative to: {this}");
		var parent = parts.Slice(0, nPartsToKeep);
		return new Path(parent.Concat(rel.relToParent.parts));
	}

	internal Path add(Sym next) => new Path(parts.rcons(next));

	internal Path parent() => new Path(parts.rtail());

	internal Sym last => parts.last;

	internal Path addExtension(string extension) {
		var b = parts.toBuilder();
		b[parts.length - 1] = Sym.of(parts[parts.length - 1].str + extension);
		return new Path(b.finish());
	}

	internal bool isEmpty => parts.isEmpty;

	/** For "a/b/c", this is "b". */
	internal Sym nameOfContainingDirectory => parts[parts.length - 1];

	internal Path directory() => new Path(parts.rtail());

	public override bool Equals(object o) => o is Path p && Equals(p);
	public bool Equals(Path p) => parts.eq(p.parts);
	public override int GetHashCode() => parts.GetHashCode();
	public override string ToString() => parts.join("/");
	public static bool operator ==(Path a, Path b) => a.Equals(b);
	public static bool operator !=(Path a, Path b) => !a.Equals(b);

	public Dat toDat() => Dat.arr(parts);
}

struct RelPath : ToData<RelPath> {
	internal readonly uint nParents;
	internal readonly Path relToParent;

	internal RelPath(uint nParents, Path relToParent) {
		this.nParents = nParents;
		this.relToParent = relToParent;
	}

	internal bool isParentsOnly => relToParent.isEmpty;
	internal Sym last => relToParent.last;

	public override bool Equals(object o) => o is RelPath r && Equals(r);
	public bool Equals(RelPath r) => nParents == r.nParents && relToParent == r.relToParent;
	public override int GetHashCode() => hashCombine(signed(nParents), relToParent.GetHashCode());
	public static bool operator ==(RelPath a, RelPath b) => a.Equals(b);
	public static bool operator !=(RelPath a, RelPath b) => !a.Equals(b);
	public Dat toDat() => Dat.of(this, nameof(nParents), Dat.num(nParents), nameof(relToParent), relToParent);
}
