using System;
using System.Text;

namespace Json {
    interface ToJson {
        void toJson(JsonWriter j);
    }

    sealed class JsonWriter {
        internal static string write(ToJson obj) => write(obj.toJson);
        internal static string write(Action<JsonWriter> act) {
            var j = new JsonWriter();
            act(j);
            return j.finish();
        }

        readonly StringBuilder sw = new StringBuilder();
        JsonWriter() {}

        struct JsonBool : ToJson {
            readonly bool value;
            internal JsonBool(bool value) { this.value = value; }
            void ToJson.toJson(JsonWriter j) {
                j.write(value.ToString());
            }
        }
        struct JsonUint : ToJson {
            readonly uint value;
            internal JsonUint(uint value) { this.value = value; }
            void ToJson.toJson(JsonWriter j) {
                j.write(value.ToString());
            }
        }
        struct JsonString : ToJson {
            readonly string value;
            internal JsonString(string value) { this.value = value; }
            void ToJson.toJson(JsonWriter j) {
                j.writeQuotedString(value);
            }
        }
        struct JsonArr<T> : ToJson where T : ToJson {
            readonly Arr<T> value;
            internal JsonArr(Arr<T> value) { this.value = value; }
            void ToJson.toJson(JsonWriter j) {
                j.writeArray(value);
            }
        }

        internal string finish() => sw.ToString();

        void write(string str) { sw.Append(str); }
        void write(char ch) { sw.Append(ch); }

        void writeArrayStart() { write('['); }
        void writeArrayEnd() { write(']'); }
        void writeComma() { write(','); }
        void writeColon() { write(':'); }

        void writeNextArrayEntry(ToJson value) {
            writeComma();
            value.toJson(this);
        }

        internal void writeArray<T>(Arr<T> xs) where T : ToJson {
            writeArrayStart();
            if (xs.length != 0) {
                xs[0].toJson(this);
                for (var i = 1; i < xs.length; i++)
                    writeComma();
                    xs[1].toJson(this);
            }
            writeArrayEnd();
        }

        internal void writeDict(string key, string value) =>
            writeDict(key, new JsonString(value));
        internal void writeDict(string key, Arr<string> value) =>
            writeDict(key, new JsonArr<JsonString>(value.map(p => new JsonString(p))));
        internal void writeDict(string key, ToJson value) {
            writeDictStart();
            writeFirstDictEntry(key, value);
            writeDictEnd();
        }

        internal void writeDict(string key1, string value1, string key2, ToJson value2) =>
            writeDict(key1, new JsonString(value1), key2, value2);
        internal void writeDict(string key1, uint value1, string key2, uint value2) =>
            writeDict(key1, new JsonUint(value1), key2, new JsonUint(value2));
        internal void writeDict(string key1, bool value1, string key2, Arr<string> value2) =>
            writeDict(key1, new JsonBool(value1), key2, new JsonArr<JsonString>(value2.map(s => new JsonString(s))));
        internal void writeDict(string key1, ToJson value1, string key2, uint value2) =>
            writeDict(key1, value1, key2, new JsonUint(value2));
        internal void writeDict<T>(string key1, string value1, string key2, Arr<T> value2) where T : ToJson =>
            writeDict(key1, new JsonString(value1), key2, new JsonArr<T>(value2));
        internal void writeDict(string key1, ToJson value1, string key2, ToJson value2) {
            writeDictStart();
            writeFirstDictEntry(key1, value1);
            writeNextDictEntry(key2, value2);
            writeDictEnd();
        }

        internal void writeDictWithMiddleThreeOptionalValues(
            string key1, ToJson value1,
            string key2, OpUint value2,
            string key3, Op<string> value3,
            string key4, Op<string> value4,
            string key5, string value5) {
            writeDictStart();
            writeFirstDictEntry(key1, value1);
            if (value2.get(out var v2))
                writeNextDictEntry(key2, new JsonUint(v2));
            if (value3.get(out var v3))
                writeNextDictEntry(key3, new JsonString(v3));
            if (value4.get(out var v4))
                writeNextDictEntry(key3, new JsonString(v4));
            writeNextDictEntry(key5, new JsonString(value5));
            writeDictEnd();
        }

        internal void writeDictWithOneOptionalValue(string key1, string value1, string key2, Op<string> value2) {
            writeDictStart();
            writeFirstDictEntry(key1, new JsonString(value1));
            if (value2.get(out var v2))
                writeNextDictEntry(key2, new JsonString(v2));
            writeDictEnd();
        }

        internal void writeDictWithTwoOptionalValues<T>(string key1, string value1, string key2, Op<string> value2, string key3, Op<Arr<T>> value3) where T : ToJson {
            writeDictStart();
            writeFirstDictEntry(key1, new JsonString(value1));
            if (value2.get(out var v2))
                writeNextDictEntry(key2, new JsonString(v2));
            if (value3.get(out var v3))
                writeNextDictEntry(key3, new JsonArr<T>(v3));
            writeDictEnd();
        }

        internal void writeDictWithTwoOptionalValues<T>(string key1, Arr<T> value1, string key2, OpUint value2, string key3, OpUint value3) where T : ToJson {
            writeDictStart();
            writeDictKey(key1);
            writeArray(value1);
            if (value2.get(out var v2))
                writeNextDictEntry(key2, new JsonUint(v2));
            if (value3.get(out var v3))
                writeNextDictEntry(key3, new JsonUint(v3));
            writeDictEnd();
        }

        internal void writeDict(
            string key1, string value1,
            string key2, uint value2,
            string key3, ToJson value3) {
            writeDict(key1, new JsonString(value1), key2, new JsonUint(value2), key3, value3);
        }

        internal void writeDict(
            string key1, string value1,
            string key2, string value2,
            string key3, ToJson value3) {
            writeDict(key1, new JsonString(value1), key2, new JsonString(value2), key3, value3);
        }
        internal void writeDict(
            string key1,
            ToJson value1,
            string key2,
            ToJson value2,
            string key3,
            ToJson value3
        ) {
            writeDictStart();
            writeFirstDictEntry(key1, value1);
            writeNextDictEntry(key2, value2);
            writeNextDictEntry(key3, value3);
            writeDictEnd();
        }

        internal void writeDict(
            string key1, uint value1,
            string key2, bool value2,
            string key3, ToJson value3,
            string key4, ToJson value4,
            string key5, bool value5,
            string key6, bool value6,
            string key7, bool value7) {
            writeDictStart();
            writeFirstDictEntry(key1, new JsonUint(value1));
            writeNextDictEntry(key2, new JsonBool(value2));
            writeNextDictEntry(key3, value3);
            writeNextDictEntry(key4, value4);
            writeNextDictEntry(key5, new JsonBool(value5));
            writeNextDictEntry(key6, new JsonBool(value6));
            writeNextDictEntry(key7, new JsonBool(value7));
        }

        void writeDictStart() { write('{'); }
        void writeDictEnd() { write('}'); }

        void writeFirstDictEntry(string key, ToJson value) {
            writeDictKey(key);
            value.toJson(this);
        }

        void writeNextDictEntry(string key, ToJson value) {
            writeComma();
            writeDictKey(key);
            value.toJson(this);
        }

        void writeDictKey(string key) {
            writeQuotedString(key);
            writeColon();
        }

        void writeQuotedString(string s) {
            write('"');
            void escape(char ch) {
                write('\\');
                write(ch);
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
                        write(ch);
                        break;
                }
            }

            write('"');
        }
    }
}

