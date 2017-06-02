using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

sealed class Path : IEquatable<Path> {
	private ImmutableArray<Sym> parts;

	public Path(ImmutableArray<Sym> parts) {
		this.parts = parts;
	}

	static Path empty = new Path(ImmutableArray.Create<Sym>());

	static Path resolveWithRoot(Path root, Path path) =>
		root.resolve(new RelPath(0, path));

	public Path from(params string[] elements) =>
		new Path(Arr.fromMappedArray(elements, Sym.of));

	public Path resolve(RelPath rel) {
		var nPartsToKeep = parts.Length - rel.nParents;
		if (nPartsToKeep < 0)
			throw new Exception($"Can't resolve: {rel}\nRelative to: {this}");
		var parent = parts.Slice(0, nPartsToKeep);
		return new Path(parent.Concat(rel.relToParent.parts));
	}

	public Path add(Sym next) => new Path(Arr.rcons(parts, next));

	public Path parent() => new Path(parts.rtail());

	public Sym last => parts.Last();

	public Path addExtension(string extension) {
		var b = parts.ToBuilder();
		b[parts.Length - 1] = Sym.of(parts[parts.Length - 1].str + extension);
		return new Path(b.ToImmutable());
	}

	public bool isEmpty =>
		parts.IsEmpty;

	public Path directory() => new Path(parts.rtail());

	//kill?
	public Tuple<Path, Sym> directoryAndBasename() => Tuple.Create(directory(), last);

	public override bool Equals(object other) => other is Path && Equals(other as Path);
	bool IEquatable<Path>.Equals(Path other) => parts.SequenceEqual(other.parts);
	public override int GetHashCode() => parts.GetHashCode();
	public override string ToString() => string.Join("/", parts);
}

sealed class RelPath {
	public readonly int nParents;
	public readonly Path relToParent;

	public RelPath(int nParents, Path relToParent) {
		Debug.Assert(nParents > 0);
		this.nParents = nParents;
		this.relToParent = relToParent;
	}

	bool isParentsOnly => relToParent.isEmpty;
	Sym last => relToParent.last;
}
