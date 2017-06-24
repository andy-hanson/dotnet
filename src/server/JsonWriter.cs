using System;

namespace Json {
	interface ToJsonSpecial {
		void toJsonSpecial(Json.JsonWriter j);
	}

	sealed class JsonWriter : Writer {
		internal static string write<T>(T obj) where T : ToData<T> {
			var j = new JsonWriter();
			j.writeObj(obj);
			return j.finish();
		}

		JsonWriter() {}

		protected override void writeObj<T>(T value) {
			if (value is ToJsonSpecial j)
				j.toJsonSpecial(this);
			else
				value.toDat().write(this);
		}

		void writeArrayStart() => writeRaw('[');
		void writeArrayEnd() => writeRaw(']');
		void writeComma() => writeRaw(',');
		void writeColon() => writeRaw(':');

		internal override void writeArray<T>(Arr<T> xs) {
			writeArrayStart();
			if (xs.length != 0) {
				writeObj(xs[0]);
				for (uint i = 1; i < xs.length; i++) {
					writeComma();
					writeObj(xs[i]);
				}
			}
			writeArrayEnd();
		}

		//kill?
		internal void writeArray<T>(Arr<T> xs, Action<T, Writer> writeValue) {
			writeArrayStart();
			if (xs.length != 0) {
				writeValue(xs[0], this);
				for (uint i = 0; i < xs.length; i++) {
					writeComma();
					writeValue(xs[i], this);
				}
			}
		}

		protected override void writeEmptyDict<T>() => writeRaw("{}");
		protected override void writeDictStart<T>() => writeDictStart();
		void writeDictStart() => writeRaw('{');
		protected override void writeDictEnd() => writeRaw('}');

		protected override void writeFirstDictEntry<T>(string key, T value) {
			writeDictKey(key);
			writeObj(value);
		}

		protected override void writeNextDictEntry<T>(string key, T value) {
			writeComma();
			writeDictKey(key);
			writeObj(value);
		}

		void writeDictKey(string key) {
			writeQuotedString(key);
			writeColon();
		}

		internal void writeDictWithMiddleThreeOptionalValues<V1>(
			string key1, V1 value1,
			string key2, OpUint value2,
			string key3, Op<string> value3,
			string key4, Op<string> value4,
			string key5, string value5)
			where V1 : ToData<V1> {
			writeDictStart();
			writeFirstDictEntry(key1, value1.toDat());
			if (value2.get(out var v2))
				writeNextDictEntry(key2, Dat.num(v2));
			if (value3.get(out var v3))
				writeNextDictEntry(key3, Dat.str(v3));
			if (value4.get(out var v4))
				writeNextDictEntry(key4, Dat.str(v4));
			writeNextDictEntry(key5, Dat.str(value5));
			writeDictEnd();
		}

		internal void writeDictWithOneOptionalValue(string key1, string value1, string key2, Op<string> value2) {
			writeDictStart();
			writeFirstDictEntry(key1, Dat.str(value1));
			if (value2.get(out var v2))
				writeNextDictEntry(key2, Dat.str(v2));
			writeDictEnd();
		}

		internal void writeDictWithTwoOptionalValues<T>(string key1, string value1, string key2, Op<string> value2, string key3, Op<Arr<T>> value3) where T : ToData<T> {
			writeDictStart();
			writeFirstDictEntry(key1, Dat.str(value1));
			if (value2.get(out var v2))
				writeNextDictEntry(key2, Dat.str(v2));
			if (value3.get(out var v3))
				writeNextDictEntry(key3, Dat.arr(v3));
			writeDictEnd();
		}

		internal void writeDictWithTwoOptionalValues<T>(string key1, Arr<T> value1, string key2, OpUint value2, string key3, OpUint value3) where T : ToData<T> {
			writeDictStart();
			writeFirstDictEntry(key1, Dat.arr(value1));
			if (value2.get(out var v2))
				writeNextDictEntry(key2, Dat.num(v2));
			if (value3.get(out var v3))
				writeNextDictEntry(key3, Dat.num(v3));
			writeDictEnd();
		}
	}
}
