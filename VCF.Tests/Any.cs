using System.Text;

namespace VCF.Tests;

/// <summary>
/// Generates placeholder random primitive data.
/// </summary>
public static class Any
{
	static Random _rand = new();
	const int stringMinLength = 0;
	const int stringMaxLength = 256;

	public static string String(int minLength = stringMinLength, int maxLength = stringMaxLength)
	{
		StringBuilder sb = new();
		do
		{
			sb.Append(Guid.NewGuid().ToString("N"));
		} while (sb.Length < maxLength);

		return sb.ToString().Substring(0, _rand.Next(minLength, maxLength));
	}

	public static (string, string) TwoStrings(int minLength = stringMinLength, int maxLength = stringMaxLength) =>
		(String(minLength, maxLength), String(minLength, maxLength));
	public static (string, string, string) ThreeStrings(int minLength = stringMinLength, int maxLength = stringMaxLength) =>
		(String(minLength, maxLength), String(minLength, maxLength), String(minLength, maxLength));
}