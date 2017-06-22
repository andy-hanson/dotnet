using static Utils;

static class Assert {
	internal static void mustEqual<T>(T a, T b) where T : ToData<T> {
		if (!a.deepEqual(b)) {
			throw TODO(); //TODO: better error msg
		}
	}
}
