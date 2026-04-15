namespace VampireCommandFramework.Registry;

internal readonly struct ParsedCommandInput
{
	// The assembly name if present, otherwise null
	internal string AssemblyName { get; }

	// The command input with assembly prefix stripped but "." prefix restored
	internal string CommandInput { get; }

	// The text after the prefix and assembly, for fuzzy matching
	internal string AfterPrefixAndAssembly { get; }

	internal bool HasAssembly => AssemblyName != null;

	internal ParsedCommandInput(string assemblyName, string commandInput, string afterPrefixAndAssembly)
	{
		AssemblyName = assemblyName;
		CommandInput = commandInput;
		AfterPrefixAndAssembly = afterPrefixAndAssembly;
	}
}
