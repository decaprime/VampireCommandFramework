namespace VampireCommandFramework;

public static class Format
{
	public enum FormatMode { GameChat, None };

	public static FormatMode Mode { get; set; } = FormatMode.GameChat;

	public static string B(string input) => Bold(input);
	public static string Bold(this string input) => Mode == FormatMode.GameChat ? $"<b>{input}</b>" : input;

	public static string I(string input) => Italic(input);

	public static string Italic(this string input) => Mode == FormatMode.GameChat ? $"<i>{input}</i>" : input;

	public static string Color(this string input, string color) => Mode == FormatMode.GameChat ? $"<color={color}>{input}</color>" : input;
}
