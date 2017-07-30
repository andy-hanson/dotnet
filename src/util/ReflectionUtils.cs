using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

using static Utils;

static class ReflectionUtils {
	internal static bool hasAttribute<TAttribute>(this Type t) where TAttribute : Attribute =>
		t.GetCustomAttribute<TAttribute>(inherit: false) != null;

	internal static bool hasAttribute<TAttribute>(this FieldInfo f) where TAttribute : Attribute =>
		f.GetCustomAttribute<TAttribute>(inherit: false) != null;

	internal static bool hasAttribute<TAttribute>(this MethodInfo m) where TAttribute : Attribute =>
		m.GetCustomAttribute<TAttribute>(inherit: false) != null;

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

	internal static Arr<ParameterInfo> paramz(this MethodInfo m) => new Arr<ParameterInfo>(m.GetParameters());
}
