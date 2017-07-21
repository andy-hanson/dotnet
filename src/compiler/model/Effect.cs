using static Utils;

namespace Model {
	/** Ordering is important; strictest level comes first, each new level contains the previous one. */
	internal enum Effect {
		/** True pure function. */
		Pure,
		/**
		Allowed to observe mutable state.
		Will return the same result if called twice *immediately*, but not if there are intervening state changes.
		*/
		Get,
		/**
		Allowed to alter state in memory.
		Not allowed to change state external to the program.
		*/
		Set,
		/**
		Allowed to interact with the outside world.
		Assumed to be async.
		*/
		Io
	}

	internal static class EffectUtils {
		/** E.g., a `set` method is allowed to `get`. */
		internal static bool contains(this Effect a, Effect b) =>
			a >= b;

		internal static Effect minCommonEffect(this Effect a, Effect b) =>
			a.contains(b) ? b : a;

		internal static string show(this Effect e) {
			switch (e) {
				case Effect.Pure: return "pure";
				case Effect.Get: return "get";
				case Effect.Set: return "set";
				case Effect.Io: return "io";
				default: throw unreachable();
			}
		}
	}
}
