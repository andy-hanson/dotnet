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

	#pragma warning disable S1186, CC0057 // empty methods, unused argument 'value'
	internal static void unused<T>(Action<T> method) {}
	internal static void unused<T, U>(Func<T, U> method) {}
	internal static void unused<T>(T value) {}
	internal static void unused<T, U>(T value1, U value2) {}
	#pragma warning restore

	internal static Exception fail(string message) =>
		new DebugFailureException(message);

	internal static Exception TODO(string message = "TODO!") {
		Debugger.Break();
		return fail(message);
	}

	internal static Exception unreachable() => fail("UNREACHABLE");

	internal static void assert(bool condition, Func<string> message) {
		if (!condition)
			throw fail(message());
	}

	internal static void assert(bool condition, string message = "Assertion failed.") {
		if (!condition)
			throw fail(message);
	}

	internal static void doTimes(uint times, Action action) {
		assert(times >= 0);
		for (var i = times; i != 0; i--)
			action();
	}

	internal static void doTimes(uint times, Action<uint> action) {
		assert(times >= 0);
		for (var i = times; i != 0; i--)
			action(i);
	}
}
