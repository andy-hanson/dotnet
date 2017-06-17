using System;
using System.Reflection;

using static Utils;

[AttributeUsage(AttributeTargets.Method)]
class TestAttribute : Attribute {}

class Program {
	static void Main(string[] args) {
		foo(typeof(TestCompile));
	}

	static void foo(Type t) {
		var methods = t.GetMethods(
			BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		foreach (var method in methods) {
			assert(method.IsStatic);
			if (method.GetCustomAttribute(typeof(TestAttribute)) == null)
				continue;

			method.Invoke(null, new object[] {});
		}
	}
}
