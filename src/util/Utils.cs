using System;
using System.Collections.Generic;
using System.Diagnostics;

static class Utils {
	internal static T upcast<T>(this T t) => t;

	internal static int unsignedDiff(uint a, uint b) =>
		checked(signed(a) - signed(b));

	internal static IComparer<T> compareBy<T>(Func<T, uint> by) =>
		Comparer<T>.Create((a, b) => unsignedDiff(by(a), by(b)));
	internal static IComparer<T> compareBy<T>(Func<T, uint> by1, Func<T, uint> by2) =>
		Comparer<T>.Create((a, b) => {
			var diff = unsignedDiff(by1(a), by1(b));
			return diff != 0 ? diff : unsignedDiff(by2(a), by2(b));
		});

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

	[DebuggerStepThrough]
	internal static int signed(uint u) => checked((int)u);
	[DebuggerStepThrough]
	internal static uint unsigned(int i) => checked((uint)i);
	[DebuggerStepThrough]
	internal static uint small(uint u) => checked((ushort)u);

	#pragma warning disable S1186, CC0057 // empty methods, unused arguments
	[DebuggerStepThrough]
	internal static void unused<T>(T value) {}
	[DebuggerStepThrough]
	internal static void unused<T, U>(T value1, U value2) {}
	#pragma warning restore

	static Exception assertionFail(string message) =>
		new DebugFailureException(message);

	internal static Exception TODO(string message = "TODO!") {
		Debugger.Break();
		return assertionFail(message);
	}

	internal static Exception unreachable() => assertionFail(nameof(unreachable));

	internal static void assert(bool condition, Func<string> message) {
		if (!condition)
			throw assertionFail(message());
	}

	internal static void assert(bool condition, Action<StringMaker> message) {
		if (!condition) {
			var sm = StringMaker.create();
			message(sm);
			throw assertionFail(sm.finish());
		}
	}

	internal static void assert(bool condition, string message = "Assertion failed.") {
		if (!condition)
			throw assertionFail(message);
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
