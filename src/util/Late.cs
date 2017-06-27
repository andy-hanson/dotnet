using System;
using System.Runtime.CompilerServices;

/**
Single-assignment value. Used when a value can't be present at initialization time and has to be added later.
WARNING: T must not be of type Op, because both Late<T> and Op<T> use default(T).
WARNING: Do not catch exceptions coming from `get` (or any assertion). Late is meant to avoid observable state.

Used like:
Late<T> _late;
internal T late { get => _late.get; set => _late.set(value); }
*/
struct Late<T> {
	T value;

	bool has => !RuntimeHelpers.Equals(value, default(T));

	internal T get {
		get {
			if (!has) throw new NullReferenceException();
			return value;
		}
	}

	internal void set(T setTo) {
		if (has) throw new Exception("Lazy value set twice.");
		value = setTo;
	}
}
