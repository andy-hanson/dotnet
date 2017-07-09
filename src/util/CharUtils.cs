static class CharUtils {
	internal static bool isNameChar(char ch) =>
		isLetter(ch) || isDigit(ch) || ch == '-';

	internal static bool isDigit(char ch) =>
		ch >= '0' && ch <= '9';

	internal static bool isLetter(char ch) =>
		isLowerCaseLetter(ch) || isUpperCaseLetter(ch);

	internal static bool isLowerCaseLetter(char ch) =>
		ch >= 'a' && ch <= 'z';

	internal static bool isUpperCaseLetter(char ch) =>
		ch >= 'A' && ch <= 'Z';
}
