using System;
using System.Diagnostics;
using System.Text;
using static System.Math;

using static Utils;

[DebuggerDisplay(":{toPathString()}")]
struct Path : ToData<Path>, IEquatable<Path> {
	readonly Arr<string> parts;

	internal Path(Arr<string> parts) {
		this.parts = parts;
	}

	static bool isPathPart(string s) {
		if (s.Length == 0)
			return false;
		foreach (var ch in s)
			switch (ch) {
				case '/':
				case '\\':
					return false;
			}
		return true;
	}

	internal static readonly Path empty = new Path(Arr.empty<string>());

	internal static Path resolveWithRoot(Path root, Path path) =>
		new Path(root.parts.Concat(path.parts));

	internal static Path fromParts(params string[] elements) {
		foreach (var e in elements)
			assert(isPathPart(e));
		return new Path(new Arr<string>(elements));
	}

	internal static Path fromString(string str) =>
		fromParts(str.Split('/'));

	internal RelPath asRel => new RelPath(0, this);

	internal Path child(string childName) {
		assert(isPathPart(childName));
		return new Path(parts.rcons(childName));
	}

	internal Path resolve(RelPath rel1, RelPath rel2) =>
		resolve(rel1).resolve(rel2);

	internal Path resolve(RelPath rel) {
		var nPartsToKeep = parts.length - rel.nParents;
		if (nPartsToKeep < 0)
			throw fail($"Can't resolve: {rel}\nRelative to: {this}");
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

		var nParents = parts.length - firstDifferentPart - 1;
		var relToParent = other.parts.slice(firstDifferentPart);
		return new RelPath(nParents, new Path(relToParent));
	}

	internal Path add(string next) => new Path(parts.rcons(next));
	internal Path add(string next, string nextNext) => new Path(parts.rcons(next, nextNext));

	internal Path parent() => new Path(parts.rtail());

	internal Op<string> opLast => isEmpty ? Op<string>.None : Op.Some(last);
	internal string last => parts.last;

	internal Path withoutExtension(string extension) {
		var lastPart = last;
		assert(lastPart.EndsWith(extension));
		var b = parts.toBuilder();
		b[parts.length - 1] = lastPart.slice(0, unsigned(lastPart.Length - extension.Length));
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

	public override bool Equals(object o) => throw new NotSupportedException();
	public override string ToString() => throw new NotSupportedException();
	public bool Equals(Path p) => deepEqual(p);
	public bool deepEqual(Path p) => parts.deepEqual(p.parts);
	public override int GetHashCode() => parts.GetHashCode();
	internal string toPathString() => parts.join("/");
	internal void toPathString(StringBuilder sb) => parts.join("/", sb);
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

	internal string toPathString() {
		var s = new StringBuilder();
		if (nParents == 0)
			s.Append("./");
		else
			doTimes(nParents, () => s.Append("../"));
		relToParent.toPathString(s);
		return s.ToString();
	}

	internal RelPath withoutExtension(string extension) =>
		new RelPath(nParents, relToParent.withoutExtension(extension));

	public override bool Equals(object o) => throw new NotSupportedException();
	public override string ToString() => throw new NotSupportedException();
	public bool deepEqual(RelPath r) => nParents == r.nParents && relToParent == r.relToParent;
	public override int GetHashCode() => throw new NotSupportedException();
	public static bool operator ==(RelPath a, RelPath b) => a.deepEqual(b);
	public static bool operator !=(RelPath a, RelPath b) => !a.deepEqual(b);
	public Dat toDat() => Dat.of(this, nameof(nParents), Dat.nat(nParents), nameof(relToParent), relToParent);
}
