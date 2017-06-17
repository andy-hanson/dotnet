using System;
using System.Collections.Generic;
using System.Diagnostics;

static class Utils {
	internal static int signed(uint u) => (int) u;
	internal static uint unsigned(int i) => (uint) i;

	internal static int hashCombine(int a, int b) =>
		a * 17 + b;

	internal static Exception TODO(string message = "TODO") {
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
}
