using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

using static Utils;

static class ReflectionUtils {
	internal static object invokeStatic(this Type t, string methodName, params object[] args) {
		var method = t.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
		assert(method != null);
		try {
			return method.Invoke(null, args);
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}
	}

	internal static object invokeInstance(this Type t, object o, string methodName, params object[] args) {
		var method = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
		try {
			return method.Invoke(o, args);
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}
	}
}
