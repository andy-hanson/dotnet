using System;

sealed class DebugFailureException : Exception {
	internal DebugFailureException(string message) : base(message) {}
}
