using System.Text;

abstract class Writer {
	readonly StringBuilder sw = new StringBuilder();

	internal void writeRaw(string str) => sw.Append(str);
	internal void writeRaw(char ch) => sw.Append(ch);
	protected string finish() => sw.ToString();

	protected abstract void writeObj<T>(T value) where T : ToData<T>; // May handle special writes

	internal void writeNull() => writeRaw("null");
	internal void writeBool(bool b) => writeRaw(b ? "true" : "false");
	internal void writeInt(int i) => writeRaw(i.ToString());
	internal void writeUint(uint u) => writeRaw(u.ToString());
	internal void writeFloat(double f) => writeRaw(f.ToString());

	protected abstract void writeEmptyDict<T>();
	protected abstract void writeDictStart<T>();
	protected abstract void writeFirstDictEntry<T>(string key, T value) where T : ToData<T>;
	protected abstract void writeNextDictEntry<T>(string key, T value) where T : ToData<T>;
	protected abstract void writeDictEnd();

	internal void writeDict<T>(Dict<string, T> d) where T : ToData<T> {
		writeDictStart<Dict<Sym, T>>();
		var first = true;
		foreach (var pair in d) {
			if (first) {
				writeFirstDictEntry(pair.Key, pair.Value);
				first = false;
			} else
				writeNextDictEntry(pair.Key, pair.Value);
		}
		writeDictEnd();
	}

	internal void writeDict<T>(Dat.Dat0<T> d) where T : ToData<T> {
		writeEmptyDict<T>();
	}
	internal void writeDict<T, V1>(Dat.Dat1<T, V1> d) where T : ToData<T> where V1 : ToData<V1> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2>(Dat.Dat2<T, V1, V2> d) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2, V3>(Dat.Dat3<T, V1, V2, V3> d) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeNextDictEntry(d.key3, d.value3);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2, V3, V4>(Dat.Dat4<T, V1, V2, V3, V4> d) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeNextDictEntry(d.key3, d.value3);
		writeNextDictEntry(d.key4, d.value4);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2, V3, V4, V5>(Dat.Dat5<T, V1, V2, V3, V4, V5> d) where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeNextDictEntry(d.key3, d.value3);
		writeNextDictEntry(d.key4, d.value4);
		writeNextDictEntry(d.key5, d.value5);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2, V3, V4, V5, V6>(Dat.Dat6<T, V1, V2, V3, V4, V5, V6> d)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeNextDictEntry(d.key3, d.value3);
		writeNextDictEntry(d.key4, d.value4);
		writeNextDictEntry(d.key5, d.value5);
		writeNextDictEntry(d.key6, d.value6);
		writeDictEnd();
	}
	internal void writeDict<T, V1, V2, V3, V4, V5, V6, V7>(Dat.Dat6<T, V1, V2, V3, V4, V5, V6, V7> d)
		where T : ToData<T> where V1 : ToData<V1> where V2 : ToData<V2> where V3 : ToData<V3> where V4 : ToData<V4> where V5 : ToData<V5> where V6 : ToData<V6> where V7 : ToData<V7> {
		writeDictStart<T>();
		writeFirstDictEntry(d.key1, d.value1);
		writeNextDictEntry(d.key2, d.value2);
		writeNextDictEntry(d.key3, d.value3);
		writeNextDictEntry(d.key4, d.value4);
		writeNextDictEntry(d.key5, d.value5);
		writeNextDictEntry(d.key6, d.value6);
		writeNextDictEntry(d.key7, d.value7);
		writeDictEnd();
	}

	internal void writeQuotedString(string s) {
		writeRaw('"');
		void escape(char ch) {
			writeRaw('\\');
			writeRaw(ch);
		}

		foreach (var ch in s) {
			switch (ch) {
				case '"':
					escape(ch);
					break;
				case '\t':
					escape('t');
					break;
				case '\n':
					escape('n');
					break;
				default:
					writeRaw(ch);
					break;
			}
		}

		writeRaw('"');
	}

	internal abstract void writeArray<T>(Arr<T> xs) where T : ToData<T>;
}
