using static Utils;

static class StringU {
    internal static char at(this string s, uint idx) =>
        s[checked ((int) idx)];

	internal static string slice(this string s, uint low, uint high) {
		return s.Substring(signed(low), signed(high - low));
	}
}
