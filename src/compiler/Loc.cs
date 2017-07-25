using System;

struct PathLoc : ToData<PathLoc> {
	internal readonly Path path;
	internal readonly Loc loc;
	internal PathLoc(Path path, Loc loc) { this.path = path; this.loc = loc; }

	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(PathLoc p) => path.deepEqual(p.path) && loc.deepEqual(p.loc);
	public Dat toDat() => Dat.of(this, nameof(path), path, nameof(loc), loc);
}

// Newtype over uint
struct Pos : ToData<Pos> {
	internal static readonly Pos start = new Pos(0);
	internal readonly uint index;
	internal Pos(uint index) { this.index = index; }

	internal Pos decr => new Pos(index - 1);
	internal Pos incr => new Pos(index + 1);

	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public static bool operator ==(Pos a, Pos b) =>
		a.index == b.index;
	public static bool operator !=(Pos a, Pos b) =>
		a.index != b.index;
	public static uint operator -(Pos a, Pos b) =>
		checked(a.index - b.index);
	public bool deepEqual(Pos pos) => index == pos.index;
	public Dat toDat() => Dat.nat(index);
}

struct Loc : ToData<Loc>, Test.ToCsonSpecial {
	internal readonly Pos start;
	internal readonly Pos end;

	internal static Loc singleChar(Pos start) => new Loc(start, start.incr);
	//TODO: eventually get rid of this
	internal static readonly Loc zero = new Loc(Pos.start, Pos.start);

	internal Loc(Pos start, Pos end) {
		this.start = start;
		this.end = end;
	}

	public override string ToString() => throw new NotSupportedException();
	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(Loc l) => start == l.start && end == l.end;
	public Dat toDat() => Dat.of(this, nameof(start), start, nameof(end), end);

	void Test.ToCsonSpecial.toCsonSpecial(Test.CsonWriter c) {
		c.writeUint(start.index);
		c.writeRaw('-');
		c.writeUint(end.index);
	}
}

struct LineAndColumn : ToData<LineAndColumn>, Show {
	internal readonly uint line;
	internal readonly uint column;
	internal LineAndColumn(uint line, uint column) {
		this.line = line;
		this.column = column;
	}

	public void show(StringMaker s) => s.add(line).add(':').add(column);
	public override string ToString() => throw new NotSupportedException();
	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(LineAndColumn l) => line == l.line && column == l.column;
	public Dat toDat() => Dat.of(this, nameof(line), Dat.nat(line), nameof(column), Dat.nat(column));
}

struct LineAndColumnLoc : ToData<LineAndColumnLoc>, Show {
	internal readonly LineAndColumn start;
	internal readonly LineAndColumn end;
	internal LineAndColumnLoc(LineAndColumn start, LineAndColumn end) {
		this.start = start;
		this.end = end;
	}

	public void show(StringMaker s) => s.add(start).add('-').add(end);
	public override string ToString() => throw new NotSupportedException();
	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(LineAndColumnLoc l) => start.deepEqual(l.start) && end.deepEqual(l.end);
	public Dat toDat() => Dat.of(this, nameof(start), end, nameof(end), end);
}

/** Lets us quickly convert a position to a line and column. */
struct LineColumnGetter {
	readonly Arr<uint> lineToPos;

	internal LineColumnGetter(string text) {
		var lineToPosBuilder = Arr.builder<uint>();
		lineToPosBuilder.add(0);
		for (uint pos = 0; pos < text.Length; pos++) {
			var ch = text.at(pos);
			if (ch == '\n')
				lineToPosBuilder.add(pos + 1);
		}
		this.lineToPos = lineToPosBuilder.finish();
	}

	internal LineAndColumnLoc lineAndColumnAtLoc(Loc loc) =>
		new LineAndColumnLoc(lineAndColumnAtPos(loc.start), lineAndColumnAtPos(loc.end));

	internal uint lineAtPos(Pos pos) =>
		lineAndColumnAtPos(pos).line;

	internal LineAndColumn lineAndColumnAtPos(Pos pos) {
		var posIndex = pos.index;
		//binary search
		uint lowLine = 0;
		var highLine = lineToPos.length - 1;

		//Invariant:
		//start of lowLineNumber comes before pos
		//end of line highLineNumber comes after pos
		while (lowLine <= highLine) {
			var middleLine = mid(lowLine, highLine);
			var middlePos = lineToPos[middleLine];

			if (middlePos == posIndex)
				return new LineAndColumn(middleLine, 0);
			else if (middlePos > posIndex)
				highLine = middleLine - 1;
			else // middlePos < posIndex
				lowLine = middleLine + 1;
		}

		var line = lowLine - 1;
		return new LineAndColumn(line, posIndex - lineToPos[line]);
	}

	static uint mid(uint a, uint b) => (a + b) / 2;
}
