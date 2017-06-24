using System;
using System.Diagnostics;

static class Utils {
	internal static void writeQuotedString(string s, Action<char> writeChar) {
		writeChar('"');
		void escape(char ch) {
			writeChar('\\');
			writeChar(ch);
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
					writeChar(ch);
					break;
			}
		}

		writeChar('"');
	}

	internal static int signed(uint u) => checked((int)u);
	internal static uint unsigned(int i) => checked((uint)i);

	internal static int hashCombine(int a, int b) =>
		a * 17 + b;

	#pragma warning disable CC0057 // unused argument 'value'
	internal static void unused<T>(Action<T> method) {}
	internal static void unused<T, U>(Func<T, U> method) {}
	internal static void unused<T>(T value) {}
	internal static void unused<T, U>(T value1, U value2) {}
	#pragma warning restore

	internal static Exception TODO(string message = "TODO!") {
		Debugger.Break();
		return new Exception(message);
	}

	internal static Exception unreachable() => new Exception("UNREACHABLE");

	internal static void assert(bool condition) {
		if (!condition) {
			throw new Exception("Assertion failed.");
		}
	}

	internal static void doTimes(uint times, Action action) {
		assert(times >= 0);
		for (var i = times; i != 0; i--) {
			action();
		}
	}

	internal static void doTimes(uint times, Action<uint> action) {
		assert(times >= 0);
		for (var i = times; i != 0; i--) {
			action(i);
		}
	}
}
