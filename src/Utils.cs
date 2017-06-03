using System;
using System.Diagnostics;
using System.Collections.Immutable;

static class Utils {
	internal static T TODO<T>(string message = "!") {
		throw new Exception(message);
	}

	internal static T nonNull<T>(T t) {
		Debug.Assert(t != null);
		return t;
	}

	internal static ImmutableArray<T> build<T>(Action<ImmutableArray<T>.Builder> builder) {
		var b = ImmutableArray.CreateBuilder<T>();
		builder(b);
		return b.ToImmutable();
	}
}
