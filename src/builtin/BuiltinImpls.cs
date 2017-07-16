namespace BuiltinImpls {
	sealed class Console : Builtins.Console {
		internal Console() {}

		Builtins.Void Builtins.Console.write_line(Builtins.String s) {
			System.Console.WriteLine(s.value);
			return Builtins.Void.instance;
		}
	}
}
