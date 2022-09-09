namespace VampireCommandFramework;

public static class Format
{
	public enum FormatMode { GameChat, None };

	public static FormatMode Mode { get; set; } = FormatMode.GameChat;

	public static string B(string input) => Bold(input);
	public static string Bold(this string input) => Mode == FormatMode.GameChat ? $"<b>{input}</b>" : input;

	public static string I(string input) => Italic(input);

	public static string Italic(this string input) => Mode == FormatMode.GameChat ? $"<i>{input}</i>" : input;
	public static string Underline(this string input) => Mode == FormatMode.GameChat ? $"<u>{input}</u>" : input;

	public static string Color(this string input, string color) => Mode == FormatMode.GameChat ? $"<color={color}>{input}</color>" : input;

	public static string Size(this string input, int size) => Mode == FormatMode.GameChat ? $"<size={size}>{input}</size>" : input;
	public static string Small(this string input) => Size(input, 10);
	public static string Normal(this string input) => Size(input, 16); // how to reset
	public static string Medium(this string input) => Size(input, 20);
	public static string Large(this string input) => Size(input, 24);

}
