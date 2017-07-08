using System;

namespace Test {
	sealed class AssertEqualException<T> : Exception where T : ToData<T> {
		internal readonly T a;
		internal readonly T b;

		internal AssertEqualException(T a, T b) : base(message(a, b)) {
			this.a = a;
			this.b = b;
		}

		static string message(T a, T b) {
			var showA = CsonWriter.write(a, initialIndent: 1);
			var showB = CsonWriter.write(b, initialIndent: 1);
			return $"Expected:\n{showA}\nActual:\n{showB}";
		}
	}

	static class Assert {
		internal static void mustEqual<T>(T a, T b) where T : ToData<T> {
			if (!a.deepEqual(b))
				throw new AssertEqualException<T>(a, b);
		}
	}
}
