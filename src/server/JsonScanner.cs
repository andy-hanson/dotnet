using System;

using static Utils;

namespace Json {
	//TODO: share with other Reader
	abstract class Reader {
		readonly string str;
		Pos idx = Pos.start;
		protected Reader(string str) { this.str = str; }

		internal char readAndSkipWhitespace() {
			var ch = readCh();
			while (isWhitespace(ch))
				ch = readCh();
			return ch;
		}

		internal char readNoWhitespace() {
			var ch = readCh();
			assert(ch != '\n');
			return ch;
		}

		internal void over() {
			while (idx.index != str.Length) {
				var ch = readCh();
				assert(!isWhitespace(ch), () => $"Did not expect {ch}");
			}
		}

		char readCh() {
			var res = str.at(idx.index);
			idx = new Pos(idx.index + 1);
			return res;
		}

		static bool isWhitespace(char ch) {
			switch (ch) {
				case ' ':
				case '\t':
				case '\r':
				case '\n':
					return true;
				default:
					return false;
			}
		}

		protected string debug() => ReaderU.debug(str, idx);
	}

	abstract class JsonScanner : Reader {
		protected JsonScanner(string source) : base(source) { }

		internal void readDictSkipDict(string key, bool last = false) {
			readDictKey(key);
			skipDict();
			dictEntryEnd(readAndSkipWhitespace(), last);
		}

		internal void skipDict() {
			readDictStart();
			var openBraces = 1;
			while (true) {
				switch (readAndSkipWhitespace()) {
					case '"':
						readStrBody();
						break;
					case '{':
						openBraces++;
						break;
					case '}':
						openBraces--;
						if (openBraces == 0)
							return;
						break;
				}
			}
		}

		internal void readComma() { expect(','); } //move near readArrayStart
		internal void readArrayStart() { expect('['); }
		internal void readArrayEnd() { expect(']'); }
		internal void readDictKey(string key) { expectKey(key); } //Why have this fn?
		internal void readDictStart() { expect('{'); }
		/** USUALLY NOT NECESSARY (if read str with 'last') */
		internal void readDictEnd() { expect('}'); }
		internal void readNull() {
			expect('n');
			expect('u');
			expect('l');
			expect('l');
		}

		internal bool readBoolean() {
			var ch = readAndSkipWhitespace();
			switch (ch) {
				case 't':
					expect('r');
					expect('u');
					expect('e');
					return true;
				case 'f':
					expect('a');
					expect('l');
					expect('s');
					expect('e');
					return false;
				default:
					throw showError("'true' or 'false'", ch);
			}
		}

		internal void readEmptyDict() {
			readDictStart();
			readDictEnd();
		}

		void dictEntryEnd(char ch, bool last) {
			expect(last ? '}' : ',', ch);
		}

		/**
		Optionally reads 'key' with an int value.
		Next key should have a string value.
		*/
		//TODO:RENAME
		internal void mayReadDictUintThenString(string key, string nextKey, bool last, out OpUint intValue, out string strValue) {
			var actualKey = readKey();
			string actualNextKey;
			if (actualKey == key) {
				intValue = OpUint.Some(readUint(readAndSkipWhitespace(), out var next));
				expect(',', next);
				actualNextKey = readKey();
			} else {
				intValue = OpUint.None;
				actualNextKey = actualKey;
			}

			assert(nextKey == actualNextKey);

			strValue = readStr();

			dictEntryEnd(readAndSkipWhitespace(), last);
		}

		internal Op<string> readDictStrOrNullEntry(string key, bool last = false) {
			expectKey(key);
			var n = readAndSkipWhitespace();
			Op<string> x;
			if (n == 'n') {
				expect('u');
				expect('l');
				expect('l');
				x = Op<string>.None;
			} else
				x = Op.Some(readStr(n));
			dictEntryEnd(readAndSkipWhitespace(), last);
			return x;
		}

		internal string readDictStrEntry(string key, bool last = false) {
			expectKey(key);
			var res = readStr();
			dictEntryEnd(readAndSkipWhitespace(), last);
			return res;
		}

		//REANDME: readDictUintEntry
		internal uint readDictUintEntry(string key, bool last = false) {
			expectKey(key);
			var res = readUint(readAndSkipWhitespace(), out var next);
			dictEntryEnd(next, last);
			return res;
		}

		private uint readUint(char fst, out char next) {
			var isDigit = toDigit(fst, out var res);
			assert(isDigit, () => $"Expected a digit, got '{fst}");

			while (true) {
				var ch = readNoWhitespace();
				if (!toDigit(ch, out var d)) {
					next = ch;
					return res;
				}
				res *= 10;
				res += d;
			}
		}

		string readStr() => readStr(readAndSkipWhitespace());

		string readStr(char fst) {
			expect('"', fst);
			return readStrBody();
		}

		string readStrBody() {
			var s = StringMaker.create();
			while (true) {
				var ch = readNoWhitespace();
				switch (ch) {
					case '\\': {
							var escaped = readNoWhitespace();
							switch (escaped) {
								case 'n': s.add('\n'); break;
								case 't': s.add('\t'); break;
								case '"': s.add('"'); break;
								default: throw TODO();
							}
							break;
						}
					case '"':
						return s.ToString();
					default:
						s.add(ch);
						break;
				}
			}
		}

		private string readKey() {
			var res = readStr();
			expect(':');
			return res;
		}

		void expectKey(string key) {
			expect('"');
			foreach (var ch in key)
				expect(ch, readNoWhitespace());
			expect('"');
			expect(':');
		}

		/*
		private Arr<T> readList<T>(char fst, Func<Char, (T, Char)> readElement) {
			expect('[', fst);

			var ch = readAndSkipWhitespace();
			if (ch == ']')
				return Arr.empty<T>();

			var res = Arr.builder<T>();
			while (true) {
				var elt = readElement(ch);
				res.add(elt.Item1);
				switch (elt.Item2) {
					case ',':
						ch = readAndSkipWhitespace();
						break;
					case ']':
						return res.finish();
					default:
						throw showError("',' or ']'", elt.Item2);
				}
			}
		}*/

		//TODO: some of these shouldn't skip whitespace
		private void expect(char expected) {
			expect(expected, readAndSkipWhitespace());
		}

		private void expect(char expected, char actual) {
			if (expected != actual)
				throw showError($"'{expected}'", actual);
		}

		private Exception showError(string expected, char actual) =>
			new DebugFailureException($"At {debug()}: Expected {expected}, got '{actual}'");

		internal static bool toDigit(char ch, out uint digit) {
			digit = ((uint)ch) - '0';
			return digit >= 0 && digit <= 9;
		}
	}
}
