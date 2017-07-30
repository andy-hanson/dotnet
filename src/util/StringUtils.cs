using static Utils;

static class StringUtils {
	internal static char at(this string s, uint idx) =>
		s[signed(idx)];

	internal static string slice(this string s, uint low, uint high) =>
		s.Substring(signed(low), signed(high - low));
	internal static string slice(this string s, uint low) =>
		slice(s, low, unsigned(s.Length));

	internal static string withoutStart(this string s, string start) {
		assert(s.StartsWith(start));
		return s.slice(unsigned(start.Length));
	}

	internal static Arr<string> split(this string s, char splitter) =>
		new Arr<string>(s.Split(splitter));

	internal static string withoutEndIfEndsWith(this string s, string end) =>
		s.EndsWith(end) ? s.slice(0, unsigned(s.Length) - unsigned(end.Length)) : s;
}
