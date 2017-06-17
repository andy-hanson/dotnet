using System;

using static Utils;

interface ToCsonSpecial {
	void toCsonSpecial(CsonWriter c);
}

sealed class CsonWriter : Writer {
	internal static string write<T>(T obj) where T : ToData<T> {
		var c = new CsonWriter();
		c.writeObj(obj);
		return c.finish();
	}

	uint indent;
	CsonWriter() {}

	protected override void writeObj<T>(T value) {
		if (value is ToCsonSpecial c)
			c.toCsonSpecial(this);
		else
			value.toDat().write(this);
	}

	protected override void writeFirstDictEntry<V>(string key, V value) => writePairCommon(key, value);
	protected override void writeNextDictEntry<V>(string key, V value) {
		writeRaw(',');
		writeLine();
		writePairCommon(key, value);
	}

	void writePairCommon<V>(string key, V value) where V : ToData<V> {
		writeRaw(key);
		writeRaw(": ");
		writeObj(value);
	}

	protected override void writeEmptyDict<T>() {
		writeType(typeof(T));
		writeRaw("()");
	}

	protected override void writeDictStart<T>() {
		writeType(typeof(T));
		writeRaw('(');
		indent++;
		writeLine();
	}

	void writeType(Type type) {
		var args = type.GenericTypeArguments;
		if (args.Length == 0)
			writeRaw(type.Name);
		else {
			var name = type.Name;
			var idx = name.IndexOf('`');
			assert(idx != -1);
			name = name.slice(0, unsigned(idx));
			writeRaw(name);
			writeRaw('<');
			writeType(args[0]);
			for (uint i = 1; i < args.Length; i++) {
				writeRaw(", ");
				writeType(args[i]);
			}
			writeRaw('>');
		}
	}

	protected override void writeDictEnd() {
		writeRaw(')');
		indent--;
	}

	void writeLine() {
		writeRaw('\n');
		doTimes(indent, () => writeRaw("  "));
	}

	internal override void writeArray<T>(Arr<T> xs) {
		switch (xs.length) {
			case 0:
				writeRaw("[]");
				return;
			case 1:
				writeRaw('[');
				writeObj(xs[0]);
				writeRaw(']');
				return;
			default:
				writeRaw('[');
				indent++;
				writeLine();
				writeObj(xs[0]);
				for (uint i = 1; i < xs.length; i++) {
					writeRaw(',');
					writeLine();
					writeObj(xs[i]);
				}
				indent--;
				writeLine();
				writeRaw(']');
				return;
		}
	}
}
