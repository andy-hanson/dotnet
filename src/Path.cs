using System;
using static System.Math;

using static Utils;

struct Path : ToData<Path>, IEquatable<Path> {
	Arr<string> parts;

	internal Path(Arr<string> parts) {
		this.parts = parts;
	}

	public static Path empty = new Path(Arr.empty<string>());

	public static Path resolveWithRoot(Path root, Path path) =>
		new Path(root.parts.Concat(path.parts));

	internal static Path from(params string[] elements) =>
		new Path(new Arr<string>(elements));

	internal static Path fromString(string str) =>
		new Path(Arr.of(str.Split('/')));

	internal RelPath asRel => new RelPath(0, this);

	internal Path resolve(RelPath rel) {
		var nPartsToKeep = parts.length - rel.nParents;
		if (nPartsToKeep < 0)
			throw new Exception($"Can't resolve: {rel}\nRelative to: {this}");
		var parent = parts.slice(0, nPartsToKeep);
		return new Path(parent.Concat(rel.relToParent.parts));
	}

	internal RelPath relTo(Path other) {
		var minLength = Min(parts.length, other.parts.length);
		uint firstDifferentPart = 0;
		for (; firstDifferentPart < minLength; firstDifferentPart++) {
			if (parts[firstDifferentPart] != other.parts[firstDifferentPart]) {
				break;
			}
		}

		var nParents = parts.length - firstDifferentPart;
		var relToParent = other.parts.slice(firstDifferentPart);
		return new RelPath(nParents, new Path(relToParent));
	}

	internal Path add(string next) => new Path(parts.rcons(next));
	internal Path add(string next, string nextNext) => new Path(parts.rcons(next, nextNext));

	internal Path parent() => new Path(parts.rtail());

	internal Op<string> opLast => isEmpty ? Op<string>.None : Op.Some(last);
	internal string last => parts.last;

	internal Path removeExtension(string extension) {
		var b = parts.toBuilder();
		var last = b[parts.length - 1];
		assert(last.EndsWith(extension));
		b[parts.length - 1] = last.slice(0, unsigned(last.Length - extension.Length));
		return new Path(new Arr<string>(b));
	}

	internal Path addExtension(string extension) {
		if (isEmpty)
			return new Path(Arr.of(extension));

		var b = parts.toBuilder();
		b[parts.length - 1] = parts[parts.length - 1] + extension;
		return new Path(new Arr<string>(b));
	}

	internal bool isEmpty => parts.isEmpty;

	/** For "a/b/c", this is "b". */
	internal string nameOfContainingDirectory => parts[parts.length - 1];

	internal Path directory() => new Path(parts.rtail());

	public override bool Equals(object o) => throw new NotImplementedException(); //o is Path p && Equals(p);
	public bool Equals(Path p) => deepEqual(p);
	public bool deepEqual(Path p) => parts.deepEqual(p.parts);
	public override int GetHashCode() => parts.GetHashCode();
	public override string ToString() => parts.join("/");
	public static bool operator ==(Path a, Path b) => a.deepEqual(b);
	public static bool operator !=(Path a, Path b) => !a.deepEqual(b);

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
	internal string last => relToParent.last;

	public override bool Equals(object o) => throw new NotImplementedException(); //o is RelPath r && Equals(r);
	public bool deepEqual(RelPath r) => nParents == r.nParents && relToParent == r.relToParent;
	public override int GetHashCode() => throw new NotImplementedException(); //hashCombine(signed(nParents), relToParent.GetHashCode());
	public static bool operator ==(RelPath a, RelPath b) => a.deepEqual(b);
	public static bool operator !=(RelPath a, RelPath b) => !a.deepEqual(b);
	public Dat toDat() => Dat.of(this, nameof(nParents), Dat.num(nParents), nameof(relToParent), relToParent);
}
