namespace VampireCommandFramework;

public interface IConverterUsage
{
	/// <summary>
	/// Returns a description of the type that this converter can parse. This is used
	/// in generated help messages.
	/// </summary>
	/// <remarks>
	/// You are expected to cache this data / make static. This property should be fast to retrieve.
	/// </remarks>
	public string Usage { get; }
}