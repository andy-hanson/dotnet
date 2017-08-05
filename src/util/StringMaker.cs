using System;
using System.Collections.Generic;
using System.Text;

using static Utils;

interface Show {
	void show<S>(S s) where S : Shower<S>;
}

interface Shower<Self> {
	Self nl();
	Self add(char ch);
	Self add(string s); // Should not contain newlines.
	Self add(uint u);
	Self add(double d);
	Self add(int i);

	Self join<T>(IEnumerable<T> arr, Action<Self, T> toString, string joiner = ", ");
	Self join<T>(Arr<T> arr, Action<Self, T> toString, string joiner = ", ");
}

struct IndentedShower<S> : Shower<IndentedShower<S>> where S : Shower<S> {
	readonly S inner;
	readonly string indent;

	internal IndentedShower(S inner, string indent) { this.inner = inner; this.indent = indent; }

	public IndentedShower<S> nl() {
		inner.add('\n');
		inner.add(indent);
		return this;
	}

	public IndentedShower<S> add(char ch) { assert(ch != '\n'); inner.add(ch); return this; }
	public IndentedShower<S> add(string s) { assert(!s.contains('\n')); inner.add(s); return this; }
	public IndentedShower<S> add(uint u) { inner.add(u); return this; }
	public IndentedShower<S> add(double d) { inner.add(d); return this; }
	public IndentedShower<S> add(int i) { inner.add(i); return this; }
	public IndentedShower<S> join<T>(IEnumerable<T> arr, Action<IndentedShower<S>, T> toString, string joiner = ", ") { throw TODO(); }
	public IndentedShower<S> join<T>(Arr<T> arr, Action<IndentedShower<S>, T> toString, string joiner = ", ") { throw TODO(); }
}

static class ShowerUtils {
	internal static S addSlice<S>(this S s, string str, uint start, uint end) where S : Shower<S> {
		for (uint i = start; i < end; i++)
			s.add(str.at(i));
		return s;
	}

	internal static S addSlice<S>(this S s, string str, uint start) where S : Shower<S> => s.addSlice(str, start, unsigned(str.Length));

	internal static S add<S, T>(this S s, T t) where S : Shower<S> where T : Show {
		t.show(s);
		return s;
	}

	internal static S join<S, T>(this S s, Arr<T> arr, Func<T, string> toString, string joiner = ", ") where S : Shower<S> =>
		s.join(arr, (ss, t) => ss.add(toString(t)), joiner);

	internal static S join<S, T>(this S s, IEnumerable<T> arr, Func<T, string> toString, string joiner = ", ") where S : Shower<S> =>
		s.join(arr, (ss, t) => ss.add(toString(t)), joiner);

	internal static S join<S, T>(this S s, Arr<T> arr, string joiner = ", ") where S : Shower<S> where T : Show =>
		s.join(arr, (ss, x) => ss.add(x), joiner);

	internal static S join<S, T>(this S s, IEnumerable<T> arr, string joiner = ", ") where S : Shower<S> where T : Show =>
		s.join(arr, (ss, x) => s.add(x), joiner);

	internal static S join<S>(this S s, Arr<string> arr, string joiner = ", ") where S : Shower<S> =>
		s.join(arr, (ss, x) => ss.add(x), joiner);

	internal static S join<S>(this S s, IEnumerable<string> xs, string joiner = ", ") where S : Shower<S> =>
		s.join(xs, (ss, x) => ss.add(x), joiner);


	internal static S showEscapedChar<S>(this S s, char ch) where S : Shower<S> {
		s.add('\'');
		switch (ch) {
			case '\n':
			case '\t':
				s.add('\\');
				goto default;
			default:
				s.add(ch);
				break;
		}
		return s.add('\'');
	}

	internal static S addQuotedString<S>(this S s, string str) where S : Shower<S> {
		#pragma warning disable CC0020 // Can't use _sb.Append, that's overloaded
		Utils.writeQuotedString(str, ch => s.add(ch));
		#pragma warning restore
		return s;
	}
}

struct StringMaker : Shower<StringMaker> {
	readonly StringBuilder sb;
	StringMaker(StringBuilder sb) { this.sb = sb; }

	public override string ToString() => throw new NotSupportedException();

	internal bool isEmpty =>
		sb.Length == 0;

	internal static StringMaker create() =>
		new StringMaker(new StringBuilder());

	internal static string stringify<T>(T t) where T : Show =>
		create().add(t).finish();

	public StringMaker nl() {
		sb.Append('\n');
		return this;
	}

	public StringMaker add(uint u) {
		sb.Append(u);
		return this;
	}

	public StringMaker add(int i) {
		sb.Append(i);
		return this;
	}

	public StringMaker add(double d) {
		sb.Append(d);
		return this;
	}

	public StringMaker add(char c) {
		sb.Append(c);
		return this;
	}

	public StringMaker add(string s) {
		sb.Append(s);
		return this;
	}

	public StringMaker join<T>(IEnumerable<T> arr, Action<StringMaker, T> toString, string joiner = ", ") {
		var e = arr.GetEnumerator();
		if (!e.MoveNext())
			return this;

		toString(this, e.Current);
		while (e.MoveNext()) {
			add(joiner);
			toString(this, e.Current);
		}
		return this;
	}

	public StringMaker join<T>(Arr<T> arr, Action<StringMaker, T> toString, string joiner = ", ") {
		if (arr.isEmpty)
			return this;

		for (uint i = 0; i < arr.length - 1; i++) {
			toString(this, arr[i]);
			add(joiner);
		}

		toString(this, arr.last);
		return this;
	}

	internal Sym finishSym() =>
		Sym.of(sb.ToString());

	internal string finish() =>
		sb.ToString();
}
