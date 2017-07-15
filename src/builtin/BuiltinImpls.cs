namespace BuiltinImpls {
	sealed class Console : Builtins.Console {
		internal Console() {}

		public override Builtins.Void write_line(Builtins.String s) {
			System.Console.WriteLine(s.value);
			return Builtins.Void.instance;
		}
	}
}
