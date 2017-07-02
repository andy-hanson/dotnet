using static Utils;

static class StringUtils {
	internal static char at(this string s, uint idx) =>
		s[signed(idx)];

	internal static string slice(this string s, uint low, uint high) =>
		s.Substring(signed(low), signed(high - low));

	internal static string showChar(char ch) {
		switch (ch) {
			case '\n': return "'\\n'";
			case '\t': return "'\\t'";
			default: return $"'{ch}'";
		}
	}
}