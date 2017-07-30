using System;
using System.Text;

interface Show {
	void show(StringMaker s);
}

struct StringMaker {
	readonly StringBuilder sb;
	StringMaker(StringBuilder sb) { this.sb = sb; }

	public override string ToString() => throw new NotSupportedException();

	internal static StringMaker create() =>
		new StringMaker(new StringBuilder());

	internal StringMaker add(uint u) {
		sb.Append(u);
		return this;
	}

	internal StringMaker add(int i) {
		sb.Append(i);
		return this;
	}

	internal StringMaker add(double d) {
		sb.Append(d);
		return this;
	}

	internal StringMaker addQuotedString(string s) {
		var sbCopy = sb; // Can't use 'this' in lambda...
		#pragma warning disable CC0020 // Can't use _sb.Append, that's overloaded
		Utils.writeQuotedString(s, ch => sbCopy.Append(ch));
		#pragma warning restore
		return this;
	}

	internal StringMaker add<T>(T t) where T : Show {
		t.show(this);
		return this;
	}

	internal StringMaker add(char c) {
		sb.Append(c);
		return this;
	}

	internal StringMaker add(string s) {
		sb.Append(s);
		return this;
	}

	internal Sym finishSym() =>
		Sym.of(sb.ToString());

	internal string finish() =>
		sb.ToString();
}