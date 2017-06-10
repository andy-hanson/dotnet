using System;
using static Utils;

struct Path : IEquatable<Path> {
	Arr<Sym> parts;

	internal Path(Arr<Sym> parts) {
		this.parts = parts;
	}

	public static Path empty = new Path(Arr.empty<Sym>());

	public static Path resolveWithRoot(Path root, Path path) =>
		new Path(root.parts.Concat(path.parts));

	internal static Path from(params string[] elements) =>
		new Path(elements.map(Sym.of));

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

	internal Path directory() => new Path(parts.rtail());

	//kill?
	internal Tuple<Path, Sym> directoryAndBasename() => Tuple.Create(directory(), last);

	public override bool Equals(object other) => other is Path && Equals((Path) other);
	bool IEquatable<Path>.Equals(Path other) => parts.eachEqual(other.parts);
	public override int GetHashCode() => parts.GetHashCode();
	public override string ToString() => parts.join("/");
}

struct RelPath {
	internal readonly uint nParents;
	internal readonly Path relToParent;

	internal RelPath(uint nParents, Path relToParent) {
		assert(nParents > 0);
		this.nParents = nParents;
		this.relToParent = relToParent;
	}

	internal bool isParentsOnly => relToParent.isEmpty;
	internal Sym last => relToParent.last;
}
