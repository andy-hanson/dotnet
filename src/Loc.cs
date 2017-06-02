using System.Collections.Immutable;

struct Loc {
    public readonly int start;
    public readonly int end;

    public static Loc singleChar(int start) => new Loc(start, start + 1);
    //TODO: eventually get rid of this
    public static readonly Loc zero = new Loc(0, 0);

    public Loc(int start, int end) {
        this.start = start;
        this.end = end;
    }
}

struct LineAndColumn {
    public readonly int line;
    public readonly int column;
    public LineAndColumn(int line, int column) {
        this.line = line;
        this.column = column;
    }

    public override string ToString() => $"{line}:{column}";
}

struct LineAndColumnLoc {
    public readonly LineAndColumn start;
    public readonly LineAndColumn end;
    public LineAndColumnLoc(LineAndColumn start, LineAndColumn end) {
        this.start = start;
        this.end = end;
    }

    public override string ToString() => $"{start}-{end}";
}

//mv
class LineColumnGetter {
    readonly string text;
    private readonly ImmutableArray<int> lineToPos;

    public LineColumnGetter(string text) {
        this.text = text;
        var lineToPos = ImmutableArray.CreateBuilder<int>();
        lineToPos.Add(0);
        for (var pos = 0; pos < text.Length; pos++) {
            var ch = text[pos];
            if (ch == '\n')
                lineToPos.Add(pos + 1);
        }
        this.lineToPos = lineToPos.ToImmutable();
    }

    public LineAndColumnLoc lineAndColumnAtLoc(Loc loc) =>
        new LineAndColumnLoc(lineAndColumnAtPos(loc.start), lineAndColumnAtPos(loc.end));

    public int lineAtPos(int pos) =>
        lineAndColumnAtPos(pos).line;

    public LineAndColumn lineAndColumnAtPos(int pos) {
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

    private static int mid(int a, int b) => (a + b) / 2;
}
