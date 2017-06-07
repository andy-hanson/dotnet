using System.Collections.Immutable;
using System.Linq;

struct PathLoc {
	internal readonly Path path;
	internal readonly Loc loc;
	internal PathLoc(Path path, Loc loc) { this.path = path; this.loc = loc; }
}

struct Loc {
	internal readonly int start;
	internal readonly int end;

	internal static Loc singleChar(int start) => new Loc(start, start + 1);
	//TODO: eventually get rid of this
	internal static readonly Loc zero = new Loc(0, 0);

	internal Loc(int start, int end) {
		this.start = start;
		this.end = end;
	}
}

struct LineAndColumn {
	internal readonly int line;
	internal readonly int column;
	internal LineAndColumn(int line, int column) {
		this.line = line;
		this.column = column;
	}

	public override string ToString() => $"{line}:{column}";
}

struct LineAndColumnLoc {
	internal readonly LineAndColumn start;
	internal readonly LineAndColumn end;
	internal LineAndColumnLoc(LineAndColumn start, LineAndColumn end) {
		this.start = start;
		this.end = end;
	}

	public override string ToString() => $"{start}-{end}";
}

struct LineColumnGetter {
	readonly string text;
	readonly ImmutableArray<int> lineToPos;

	internal LineColumnGetter(string text) {
		this.text = text;
		var lineToPos = ImmutableArray.CreateBuilder<int>();
		lineToPos.Add(0);
		foreach (var pos in Enumerable.Range(0, text.Length)) {
			var ch = text[pos];
			if (ch == '\n')
				lineToPos.Add(pos + 1);
		}
		this.lineToPos = lineToPos.ToImmutable();
	}

	internal LineAndColumnLoc lineAndColumnAtLoc(Loc loc) =>
		new LineAndColumnLoc(lineAndColumnAtPos(loc.start), lineAndColumnAtPos(loc.end));

	internal int lineAtPos(int pos) =>
		lineAndColumnAtPos(pos).line;

	internal LineAndColumn lineAndColumnAtPos(int pos) {
		//binary search
		var lowLine = 0;
		var highLine = lineToPos.Length - 1;

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

		var line = lowLine - 1;
		return new LineAndColumn(line, pos - lineToPos[line]);
	}

	static int mid(int a, int b) => (a + b) / 2;
}
