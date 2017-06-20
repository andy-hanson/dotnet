using System;

struct PathLoc : ToData<PathLoc> {
	internal readonly Path path;
	internal readonly Loc loc;
	internal PathLoc(Path path, Loc loc) { this.path = path; this.loc = loc; }

	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
	public bool deepEqual(PathLoc p) => path.Equals(p.path) && loc.Equals(p.loc);
	public Dat toDat() => Dat.of(this, nameof(path), path, nameof(loc), loc);
}

struct Loc : ToData<Loc>, ToCsonSpecial {
	internal readonly uint start;
	internal readonly uint end;

	internal static Loc singleChar(uint start) => new Loc(start, start + 1);
	//TODO: eventually get rid of this
	internal static readonly Loc zero = new Loc(0, 0);

	internal Loc(uint start, uint end) {
		this.start = start;
		this.end = end;
	}

	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
	public bool deepEqual(Loc l) => start == l.start && end == l.end;
	public Dat toDat() => Dat.of(this, nameof(start), Dat.num(start), nameof(end), Dat.num(end));

	void ToCsonSpecial.toCsonSpecial(CsonWriter c) {
		c.writeUint(start);
		c.writeRaw('-');
		c.writeUint(end);
	}
}

struct LineAndColumn : ToData<LineAndColumn> {
	internal readonly uint line;
	internal readonly uint column;
	internal LineAndColumn(uint line, uint column) {
		this.line = line;
		this.column = column;
	}

	public override string ToString() => $"{line}:{column}";
	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
	public bool deepEqual(LineAndColumn l) => line == l.line && column == l.column;
	public Dat toDat() => Dat.of(this, nameof(line), Dat.num(line), nameof(column), Dat.num(column));
}

struct LineAndColumnLoc : ToData<LineAndColumnLoc> {
	internal readonly LineAndColumn start;
	internal readonly LineAndColumn end;
	internal LineAndColumnLoc(LineAndColumn start, LineAndColumn end) {
		this.start = start;
		this.end = end;
	}

	public override string ToString() => $"{start}-{end}";
	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
	public bool deepEqual(LineAndColumnLoc l) => start.Equals(l.start) && end.Equals(l.end);
	public Dat toDat() => Dat.of(this, nameof(start), end, nameof(end), end);
}

/** Lets us quickly convert a position to a line and column. */
struct LineColumnGetter {
	readonly Arr<uint> lineToPos;

	internal LineColumnGetter(string text) {
		var lineToPos = Arr.builder<uint>();
		lineToPos.add(0);
		for (uint pos = 0; pos < text.Length; pos++) {
			var ch = text.at(pos);
			if (ch == '\n')
				lineToPos.add(pos + 1);
		}
		this.lineToPos = lineToPos.finish();
	}

	internal LineAndColumnLoc lineAndColumnAtLoc(Loc loc) =>
		new LineAndColumnLoc(lineAndColumnAtPos(loc.start), lineAndColumnAtPos(loc.end));

	internal uint lineAtPos(uint pos) =>
		lineAndColumnAtPos(pos).line;

	internal LineAndColumn lineAndColumnAtPos(uint pos) {
		//binary search
		uint lowLine = 0;
		uint highLine = lineToPos.length - 1;

		//Invariant:
		//start of lowLineNumber comes before pos
		//end of line highLineNumber comes after pos
		while (lowLine <= highLine) {
			var middleLine = mid(lowLine, highLine);
			var middlePos = lineToPos[middleLine];

			if (middlePos == pos)
				return new LineAndColumn(middleLine, 0);
			else if (pos < middlePos)
				highLine = middleLine - 1;
			else // pos > middlePos
				lowLine = middleLine + 1;
		}

		uint line = lowLine - 1;
		return new LineAndColumn(line, pos - lineToPos[line]);
	}

	static uint mid(uint a, uint b) => (a + b) / 2;
}
