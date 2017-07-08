using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

using static Utils;

static class ReflectionUtils {
	internal static object invokeStatic(this Type t, string methodName, params object[] args) {
		try {
			return t.GetMethod(methodName).Invoke(null, args);
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}
	}
}
