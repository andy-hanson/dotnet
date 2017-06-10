using static Utils;

static class StringU {
	internal static string slice(this string s, uint low, uint high) {
		return s.Substring(signed(low), signed(high - low));
	}
}
