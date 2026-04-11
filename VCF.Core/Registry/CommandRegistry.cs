using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using VampireCommandFramework.Basics;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

using static VampireCommandFramework.Format;

namespace VampireCommandFramework;

public static class CommandRegistry
{
	internal const string DEFAULT_PREFIX = ".";
	internal static CommandCache _cache = new();
	/// <summary>
	/// From converting type to (object instance, MethodInfo tryParse, Type contextType)
	/// </summary>
	internal static Dictionary<Type, (object instance, MethodInfo tryParse, Type contextType)> _converters = new();

	internal static void Reset()
	{
		// testability and a bunch of static crap, I know...
		Middlewares.Clear();
		Middlewares.AddRange(DEFAULT_MIDDLEWARES);
		AssemblyCommandMap.Clear();
		_converters.Clear();
		_cache = new();
		CommandHistory.Reset();
		_pendingCommands.Clear();
	}

	// todo: document this default behavior, it's just not something to ship without but you can Middlewares.Claer();
	private static List<CommandMiddleware> DEFAULT_MIDDLEWARES = new() { new VCF.Core.Basics.BasicAdminCheck() };
	public static List<CommandMiddleware> Middlewares { get; } = new() { new VCF.Core.Basics.BasicAdminCheck() };

	// Store pending commands for selection
	private static Dictionary<string, (string input, List<(CommandMetadata Command, object[] Args, string Error)> commands)> _pendingCommands = new();

	internal static ParsedCommandInput ParseInput(string input)
	{
		string afterPrefix = input.Substring(DEFAULT_PREFIX.Length);
		int spaceIndex = afterPrefix.IndexOf(' ');
		if (spaceIndex > 0)
		{
			string potentialAssemblyName = afterPrefix.Substring(0, spaceIndex);
			bool isValidAssembly = AssemblyCommandMap.Keys.Any(an =>
				an.Equals(potentialAssemblyName, StringComparison.OrdinalIgnoreCase));
			if (isValidAssembly)
			{
				string afterAssembly = afterPrefix.Substring(spaceIndex + 1);
				string commandInput = DEFAULT_PREFIX + afterAssembly;

				// Only treat as assembly-qualified if that assembly has a matching command
				var assemblyMatch = _cache.GetCommandFromAssembly(commandInput, potentialAssemblyName);
				if (assemblyMatch != null && assemblyMatch.IsMatched)
				{
					return new ParsedCommandInput(potentialAssemblyName, commandInput, afterAssembly);
				}
			}
		}
		return new ParsedCommandInput(null, input, afterPrefix);
	}

	internal static CacheResult GetCommandFromCache(string input, string assemblyName = null)
	{
		if (assemblyName != null)
		{
			return _cache.GetCommandFromAssembly(input, assemblyName);
		}
		return _cache.GetCommand(input);
	}

	internal static bool CanCommandExecute(ICommandContext ctx, CommandMetadata command)
	{
		// Log.Debug($"Executing {Middlewares.Count} CanHandle Middlwares:");
		foreach (var middleware in Middlewares)
		{
			// Log.Debug($"\t{middleware.GetType().Name}");
			try
			{
				if (!middleware.CanExecute(ctx, command.Attribute, command.Method))
				{
					return false;
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error executing {middleware.GetType().Name.Color(Color.Gold)} {e}");
				return false;
			}
		}
		return true;
	}

	internal static bool HasRemainderParameter(CommandMetadata command)
	{
		if (command.Parameters.Length == 0) return false;
		
		var lastParam = command.Parameters[command.Parameters.Length - 1];
		return lastParam.Name == "_remainder" && lastParam.ParameterType == typeof(string);
	}

	internal static IEnumerable<string> FindCloseMatches(ICommandContext ctx, string input)
	{
		// Look for the closest matches to the input command
		const int maxResults = 3;

		// Set a more reasonable max distance
		const int maxFixedDistance = 3;  // For short to medium commands
		const double maxRelativeDistance = 0.5; // Max 50% of command length can be different

		// Ensure we have a valid input
		if (string.IsNullOrWhiteSpace(input))
		{
			return Enumerable.Empty<string>();
		}

		// Remove the prefix (and assembly if present) to match command names better
		var parsed = ParseInput(input);
		var normalizedInput = parsed.AfterPrefixAndAssembly.ToLowerInvariant();

		var maxDistance = Math.Max(maxFixedDistance,
								(int)Math.Ceiling(normalizedInput.Length * maxRelativeDistance));

		// Get all registered commands for comparison
		var allCommands = AssemblyCommandMap.SelectMany(a => a.Value.Keys).ToList();

		// Calculate edit distances and select the closest matches
		var matches = allCommands
			.Where(c => CanCommandExecute(ctx, c))
			.SelectMany(cmd =>
			{
				// Get all possible combinations of group and command names
				var groupNames = cmd.GroupAttribute == null
					? new[] { "" }
					: cmd.GroupAttribute.ShortHand == null
						? new[] { cmd.GroupAttribute.Name + " " }
						: new[] { cmd.GroupAttribute.Name + " ", cmd.GroupAttribute.ShortHand + " " };

				var commandNames = cmd.Attribute.ShortHand == null
					? new[] { cmd.Attribute.Name }
					: new[] { cmd.Attribute.Name, cmd.Attribute.ShortHand };

				return groupNames.SelectMany(group =>
					commandNames.Select(name => new
					{
						FullName = (group + name).ToLowerInvariant(),
						Command = cmd
					}));
			})
			.Select(cmdInfo =>
			{
				// Calculate the Damerau-Levenshtein distance
				var distance = DamerauLevenshteinDistance(normalizedInput, cmdInfo.FullName);

				var maxCmdDistance = Math.Max(maxDistance, (int)Math.Ceiling(normalizedInput.Length * maxRelativeDistance));

				return new { Command = cmdInfo.FullName, Distance = distance, MaxDistance = maxCmdDistance };
			})
			.Where(x => x.Distance <= x.MaxDistance) // Apply adaptive threshold
			.OrderBy(x => x.Distance)
			.DistinctBy(x => x.Command)
			.Take(maxResults)
			.Select(x => "." + x.Command);

		return matches;
	}

	private static float DamerauLevenshteinDistance(string s, string t)
	{
		// Handle edge cases
		if (string.IsNullOrEmpty(s))
			return string.IsNullOrEmpty(t) ? 0 : t.Length;
		if (string.IsNullOrEmpty(t))
			return s.Length;

		// Create distance matrix
		float[,] matrix = new float[s.Length + 1, t.Length + 1];

		// Initialize first row and column
		for (int i = 0; i <= s.Length; i++)
			matrix[i, 0] = i;

		for (int j = 0; j <= t.Length; j++)
			matrix[0, j] = j;

		// Calculate distances
		for (int i = 1; i <= s.Length; i++)
		{
			for (int j = 1; j <= t.Length; j++)
			{
				int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;

				// Standard Levenshtein operations: deletion, insertion, substitution
				matrix[i, j] = Math.Min(Math.Min(
					matrix[i - 1, j] + 1,      // Deletion
					matrix[i, j - 1] + 1),     // Insertion
					matrix[i - 1, j - 1] + 1.5f*cost); // Substitution (slightly higher than just missing/extra letters)

				// Add transposition check (swap)
				if (i > 1 && j > 1 &&
					s[i - 1] == t[j - 2] &&
					s[i - 2] == t[j - 1])
				{
					matrix[i, j] = Math.Min(matrix[i, j],
						matrix[i - 2, j - 2] + cost); // Transposition
				}
			}
		}

		return matrix[s.Length, t.Length];
	}

	private static void HandleBeforeExecute(ICommandContext ctx, CommandMetadata command)
	{
		Middlewares.ForEach(m => m.BeforeExecute(ctx, command.Attribute, command.Method));
	}

	private static void HandleAfterExecute(ICommandContext ctx, CommandMetadata command)
	{
		Middlewares.ForEach(m => m.AfterExecute(ctx, command.Attribute, command.Method));
	}

	public static CommandResult Handle(ICommandContext ctx, string input)
	{
		// Load command history for this user if it's their first command this session
		CommandHistory.EnsureHistoryLoaded(ctx);

		// Check if this is a command selection (e.g., .1, .2, etc.)
		if (input.StartsWith(DEFAULT_PREFIX) && input.Length > 1)
		{
			string numberPart = input.Substring(1);
			if (int.TryParse(numberPart, out int selectedIndex) && selectedIndex > 0)
			{
				return HandleCommandSelection(ctx, selectedIndex);
			}
		}

		// Ensure the command starts with the prefix
		if (!input.StartsWith(DEFAULT_PREFIX))
		{
			return CommandResult.Unmatched; // Not a command
		}

		if (input.Trim().StartsWith(".!"))
		{
			return CommandHistory.HandleHistoryCommand(ctx, input.Trim(), Handle, ExecuteCommandWithArgs);
		}

		// Parse assembly prefix, command, and remainder in one place
		var parsed = ParseInput(input);

		// Get command(s) based on input
		CacheResult matchedCommand = null;
		if (parsed.HasAssembly)
		{
			matchedCommand = _cache.GetCommandFromAssembly(parsed.CommandInput, parsed.AssemblyName);
		}
		if (matchedCommand == null || !matchedCommand.IsMatched)
		{
			matchedCommand = _cache.GetCommand(input);
		}

		var commands = matchedCommand.Commands;

		if (!matchedCommand.IsMatched)
		{
			if (!matchedCommand.HasPartial) return CommandResult.Unmatched; // NOT FOUND

			foreach (var possible in matchedCommand.PartialMatches)
			{
				ctx.SysReply(HelpCommands.GetShortHelp(possible));
			}

			return CommandResult.UsageError;
		}

		// If there's only one command, handle it directly
		if (commands.Count() == 1)
		{
			var (command, args) = commands.First();
			return ExecuteCommand(ctx, command, args, parsed.CommandInput);
		}

		// Multiple commands match, try to convert parameters for each
		var successfulCommands = new List<(CommandMetadata Command, object[] Args, string Error)>();
		var failedCommands = new List<(CommandMetadata Command, string Error)>();

		foreach (var (command, args) in commands)
		{
			if (!CanCommandExecute(ctx, command)) continue;

			var (success, commandArgs, error) = TryConvertParameters(ctx, command, args, parsed.CommandInput);
			if (success)
			{
				successfulCommands.Add((command, commandArgs, null));
			}
			else
			{
				failedCommands.Add((command, error));
			}
		}

		// Case 1: No command succeeded
		if (successfulCommands.Count == 0)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"{"[error]".Color(Color.Red)} Failed to execute command due to parameter conversion errors:");
			foreach (var (command, error) in failedCommands)
			{
				string assemblyInfo = command.AssemblyName;
				sb.AppendLine($"  - {command.Attribute.Name} ({assemblyInfo}): {error}");
			}
			ctx.SysPaginatedReply(sb);
			return CommandResult.UsageError;
		}

		// Case 2: Only one command succeeded
		if (successfulCommands.Count == 1)
		{
			var (command, commandArgs, _) = successfulCommands[0];
			CommandHistory.AddToHistory(ctx, input, command, commandArgs);
			return ExecuteCommandWithArgs(ctx, command, commandArgs);
		}

		// Case 3: Multiple commands succeeded - store and ask user to select
		var pendingKey = ctx.Name;
		_pendingCommands[pendingKey] = (input, successfulCommands);

		{
			var sb = new StringBuilder();
			sb.AppendLine($"Multiple commands match this input. Select one by typing {B(".<#>").Color(Color.Command)}:");
			for (int i = 0; i < successfulCommands.Count; i++)
			{
				var (command, _, _) = successfulCommands[i];
				var cmdAssembly = command.AssemblyName;
				var description = command.Attribute.Description;
				sb.AppendLine($" {("." + (i + 1).ToString()).Color(Color.Command)} - {cmdAssembly.Bold().Color(Color.Primary)} - {B(command.Attribute.Name)} {command.Attribute.Description}");
				sb.AppendLine("   " + HelpCommands.GetShortHelp(command));
			}
			ctx.SysPaginatedReply(sb);
		}

		return CommandResult.Success;
	}

	// Add these helper methods:

	private static CommandResult HandleCommandSelection(ICommandContext ctx, int selectedIndex)
	{
		var pendingKey = ctx.Name;
		
		if (!_pendingCommands.TryGetValue(pendingKey, out var pendingCommands) || pendingCommands.commands.Count == 0)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} No command selection is pending.");
			return CommandResult.CommandError;
		}

		if (selectedIndex < 1 || selectedIndex > pendingCommands.commands.Count)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} Invalid selection. Please select a number between {"1".Color(Color.Gold)} and {pendingCommands.commands.Count.ToString().Color(Color.Gold)}.");
			return CommandResult.UsageError;
		}

		var (command, args, _) = pendingCommands.commands[selectedIndex - 1];

		CommandHistory.AddToHistory(ctx, pendingCommands.input, command, args);
		var result = ExecuteCommandWithArgs(ctx, command, args);
		_pendingCommands.Remove(pendingKey);
		return result;
	}

	internal static (bool Success, object[] Args, string Error) TryConvertParameters(ICommandContext ctx, CommandMetadata command, string[] args, string originalInput = null)
	{
		var argCount = args?.Length ?? 0;
		var paramsCount = command.Parameters.Length;
		var commandArgs = new object[paramsCount + 1];
		commandArgs[0] = ctx;

		bool hasRemainder = HasRemainderParameter(command);

		// Special case for commands with no parameters
		if (paramsCount == 0 && argCount == 0)
		{
			return (true, commandArgs, null);
		}

		// Handle remainder commands with special logic
		if (hasRemainder)
		{
			return TryConvertParametersWithRemainder(ctx, command, args, originalInput, commandArgs);
		}

		// Handle parameter count mismatch for non-remainder commands
		if (argCount > paramsCount)
		{
			return (false, null, $"Too many parameters: expected {paramsCount.ToString().Color(Color.Gold)}, got {argCount.ToString().Color(Color.Gold)}");
		}

		// Handle missing parameters for non-remainder commands
		if (argCount < paramsCount)
		{
			var missingParams = command.Parameters.Skip(argCount);
			var canDefault = missingParams.All(p => p.HasDefaultValue);
			if (!canDefault)
			{
				return (false, null, $"Missing required parameters: expected {paramsCount.ToString().Color(Color.Gold)}, got {argCount.ToString().Color(Color.Gold)}");
			}
			
			for (var i = argCount; i < paramsCount; i++)
			{
				commandArgs[i + 1] = command.Parameters[i].DefaultValue;
			}
		}

		// Convert provided arguments for non-remainder commands
		for (var i = 0; i < Math.Min(argCount, paramsCount); i++)
		{
			var param = command.Parameters[i];
			var arg = args[i];
			
			var (success, convertedValue, error) = TryConvertSingleParameter(ctx, param, arg, i);
			if (!success)
			{
				return (false, null, error);
			}
			
			commandArgs[i + 1] = convertedValue;
		}

		return (true, commandArgs, null);
	}

	private static (bool Success, object[] Args, string Error) TryConvertParametersWithRemainder(ICommandContext ctx, CommandMetadata command, string[] args, string originalInput, object[] commandArgs)
	{
		var argCount = args?.Length ?? 0;
		var paramsCount = command.Parameters.Length;
		var remainderIndex = paramsCount - 1; // _remainder is always last
		
		// Calculate minimum required parameters (non-optional, non-remainder)
		var requiredParamCount = 0;
		for (int i = 0; i < remainderIndex; i++)
		{
			if (!command.Parameters[i].HasDefaultValue)
			{
				requiredParamCount++;
			}
		}

		// Check if we have enough arguments for required parameters
		if (argCount < requiredParamCount)
		{
			return (false, null, $"Missing required parameters: expected at least {requiredParamCount.ToString().Color(Color.Gold)}, got {argCount.ToString().Color(Color.Gold)}");
		}

		// Try different strategies to split arguments between regular params and remainder
		// Start from the maximum possible and work backwards to handle optional parameters
		var maxNonRemainderArgs = Math.Min(argCount, remainderIndex);
		
		for (int splitPoint = maxNonRemainderArgs; splitPoint >= requiredParamCount; splitPoint--)
		{
			var (success, error) = TryConvertWithSplitPoint(ctx, command, args, originalInput, commandArgs, splitPoint);
			if (success)
			{
				return (true, commandArgs, null);
			}
			
			// If conversion failed due to parameter type mismatch and we have optional parameters,
			// try with fewer parameters (let optional parameters use defaults)
			if (error != null && error.Contains("Parameter") && splitPoint > requiredParamCount)
			{
				continue; // Try next split point
			}
			
			// If it's a different kind of error or we're at minimum required, return it
			if (splitPoint == requiredParamCount)
			{
				return (false, null, error);
			}
		}
		
		return (false, null, "Failed to parse parameters");
	}

	private static (bool Success, string Error) TryConvertWithSplitPoint(ICommandContext ctx, CommandMetadata command, string[] args, string originalInput, object[] commandArgs, int splitPoint)
	{
		var paramsCount = command.Parameters.Length;
		var remainderIndex = paramsCount - 1;
		
		// Convert regular parameters up to split point
		for (int i = 0; i < splitPoint; i++)
		{
			var param = command.Parameters[i];
			var arg = args[i];
			
			var (success, convertedValue, error) = TryConvertSingleParameter(ctx, param, arg, i);
			if (!success)
			{
				return (false, error);
			}
			
			commandArgs[i + 1] = convertedValue;
		}
		
		// Fill remaining optional parameters with defaults
		for (int i = splitPoint; i < remainderIndex; i++)
		{
			var param = command.Parameters[i];
			if (param.HasDefaultValue)
			{
				commandArgs[i + 1] = param.DefaultValue;
			}
			else
			{
				return (false, $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}) is required but no value provided");
			}
		}
		
		// Handle remainder parameter
		if (!string.IsNullOrEmpty(originalInput))
		{
			commandArgs[remainderIndex + 1] = ExtractRemainderFromOriginalInput(originalInput, command, remainderIndex, splitPoint);
		}
		else
		{
			var remainderArgs = args.Skip(splitPoint).ToArray();
			commandArgs[remainderIndex + 1] = string.Join(" ", remainderArgs);
		}
		
		return (true, null);
	}

	private static (bool Success, object ConvertedValue, string Error) TryConvertSingleParameter(ICommandContext ctx, ParameterInfo param, string arg, int paramIndex)
	{
		try
		{
			// Custom Converter
			if (_converters.TryGetValue(param.ParameterType, out var customConverter))
			{
				var (converter, convertMethod, converterContextType) = customConverter;

				if (!converterContextType.IsAssignableFrom(ctx.GetType()))
				{
					return (false, null, $"INTERNAL_ERROR:Converter type {converterContextType.Name.ToString().Color(Color.Gold)} is not assignable from {ctx.GetType().Name.ToString().Color(Color.Gold)}");
				}

				var tryParseArgs = new object[] { ctx, arg };
				try
				{
					var result = convertMethod.Invoke(converter, tryParseArgs);
					return (true, result, null);
				}
				catch (TargetInvocationException tie)
				{
					if (tie.InnerException is CommandException e)
					{
						return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): {e.Message}");
					}
					else
					{
						return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error converting parameter");
					}
				}
				catch (Exception)
				{
					return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error converting parameter");
				}
			}
			else
			{
				var defaultConverter = TypeDescriptor.GetConverter(param.ParameterType);
				try
				{
					var val = defaultConverter.ConvertFromInvariantString(arg);

					// Separate, more robust enum validation
					if (param.ParameterType.IsEnum)
					{
						bool isDefined = false;

						// For numeric input, we need to check if the value is defined
						if (int.TryParse(arg, out int enumIntVal))
						{
							isDefined = Enum.IsDefined(param.ParameterType, enumIntVal);

							if (!isDefined)
							{
								return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): Invalid enum value '{arg.ToString().Color(Color.Gold)}' for {param.ParameterType.Name.ToString().Color(Color.Gold)}");
							}
						}
					}

					return (true, val, null);
				}
				catch (Exception e)
				{
					return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): {e.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			return (false, null, $"Parameter {paramIndex + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error: {ex.Message}");
		}
	}

	private static string ExtractRemainderFromOriginalInput(string originalInput, CommandMetadata command, int remainderParameterIndex, int splitPoint = -1)
	{
		// Remove the prefix (.)
		var afterPrefix = originalInput.Substring(DEFAULT_PREFIX.Length);

		// Count words to skip by detecting which name variant (full or shorthand) was used in the input
		int commandWordCount = 0;

		if (command.GroupAttribute != null)
		{
			var groupName = command.GroupAttribute.Name;
			var groupShortHand = command.GroupAttribute.ShortHand;

			if (groupShortHand != null && afterPrefix.StartsWith(groupShortHand + " ", StringComparison.OrdinalIgnoreCase))
				commandWordCount += groupShortHand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
			else
				commandWordCount += groupName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
		}

		var cmdName = command.Attribute.Name;
		var cmdShortHand = command.Attribute.ShortHand;

		if (cmdShortHand != null)
		{
			// Skip past the group words to check which command name variant follows
			var checkPos = 0;
			for (int w = 0; w < commandWordCount; w++)
			{
				while (checkPos < afterPrefix.Length && afterPrefix[checkPos] != ' ') checkPos++;
				while (checkPos < afterPrefix.Length && afterPrefix[checkPos] == ' ') checkPos++;
			}
			var afterGroup = afterPrefix.Substring(checkPos);

			if (afterGroup.StartsWith(cmdShortHand + " ", StringComparison.OrdinalIgnoreCase)
				|| afterGroup.Equals(cmdShortHand, StringComparison.OrdinalIgnoreCase))
				commandWordCount += cmdShortHand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
			else
				commandWordCount += cmdName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
		}
		else
		{
			commandWordCount += cmdName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
		}

		var pos = 0;
		for (int w = 0; w < commandWordCount; w++)
		{
			while (pos < afterPrefix.Length && afterPrefix[pos] != ' ') pos++;
			while (pos < afterPrefix.Length && afterPrefix[pos] == ' ') pos++;
		}

		if (pos >= afterPrefix.Length)
			return "";

		var parametersText = afterPrefix.Substring(pos);

		// If remainder is the first parameter, return all parameters
		if (remainderParameterIndex == 0)
		{
			return parametersText;
		}

		// We need to skip the first splitPoint parameters (or remainderParameterIndex if splitPoint not provided)
		var parametersToSkip = splitPoint >= 0 ? splitPoint : remainderParameterIndex;
		
		// Parse through the parameters text respecting quotes
		var currentParamIndex = 0;
		var position = 0;
		var inQuotes = false;

		while (position < parametersText.Length && currentParamIndex < parametersToSkip)
		{
			var ch = parametersText[position];
		
			// Handle escaped quotes
			if (ch == '\\' && position + 1 < parametersText.Length && parametersText[position + 1] == '"')
			{
				position += 2;
				continue;
			}
		
			if (ch == '"')
			{
				inQuotes = !inQuotes;
			}
			else if (ch == ' ' && !inQuotes)
			{
				// Skip consecutive spaces
				while (position < parametersText.Length && parametersText[position] == ' ')
				{
					position++;
				}
				currentParamIndex++;
				continue;
			}
		
			position++;
		}

		// Return the remainder from this position
		if (position < parametersText.Length)
		{
			return parametersText.Substring(position);
		}

		return "";
	}

	private static CommandResult ExecuteCommand(ICommandContext ctx, CommandMetadata command, string[] args, string input)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ContextType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
			return CommandResult.InternalError;
		}

		// Try to convert parameters
		var (success, commandArgs, error) = TryConvertParameters(ctx, command, args, input);
		if (!success)
		{
			// Check for special internal error flag
			if (error != null && error.StartsWith("INTERNAL_ERROR:"))
			{
				string actualError = error.Substring("INTERNAL_ERROR:".Length);
				Log.Warning(actualError);
				ctx.InternalError();
				return CommandResult.InternalError;
			}

			ctx.SysReply($"{"[error]".Color(Color.Red)} {error}");
			return CommandResult.UsageError;
		}

		CommandHistory.AddToHistory(ctx, input, command, commandArgs);
		return ExecuteCommandWithArgs(ctx, command, commandArgs);
	}

	private static CommandResult ExecuteCommandWithArgs(ICommandContext ctx, CommandMetadata command, object[] commandArgs)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ContextType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
			return CommandResult.InternalError;
		}

		// Then handle this invocation's context not being valid for the command classes custom constructor
		if (command.Constructor != null && !command.ConstructorType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ConstructorType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
			ctx.InternalError();
			return CommandResult.InternalError;
		}

		object instance = null;
		// construct command's type with context if declared only in a non-static class and on a non-static method
		if (!command.Method.IsStatic && !(command.Method.DeclaringType.IsAbstract && command.Method.DeclaringType.IsSealed))
		{
			try
			{
				instance = command.Constructor == null ? Activator.CreateInstance(command.Method.DeclaringType) : command.Constructor.Invoke(new[] { ctx });
			}
			catch (TargetInvocationException tie)
			{
				if (tie.InnerException is CommandException ce)
				{
					ctx.SysReply(ce.Message);
				}
				else
				{
					ctx.InternalError();
				}

				return CommandResult.InternalError;
			}
		}

		// Handle Middlewares
		if (!CanCommandExecute(ctx, command))
		{
			ctx.SysReply($"{"[denied]".Color(Color.Red)} {command.Attribute.Name.ToString().Color(Color.Gold)}");
			return CommandResult.Denied;
		}

		HandleBeforeExecute(ctx, command);

		// Execute Command
		try
		{
			command.Method.Invoke(instance, commandArgs);
		}
		catch (TargetInvocationException tie) when (tie.InnerException is CommandException e)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} {e.Message}");
			return CommandResult.CommandError;
		}
		catch (Exception e)
		{
			Log.Warning($"Hit unexpected exception executing command {command.Attribute.Id.ToString().Color(Color.Gold)}\n: {e}");
			ctx.InternalError();
			return CommandResult.InternalError;
		}

		HandleAfterExecute(ctx, command);

		return CommandResult.Success;
	}

	public static void UnregisterConverter(Type converter)
	{
		if (!IsGenericConverterContext(converter) && !IsSpecificConverterContext(converter))
		{
			return;
		}

		var args = converter.BaseType.GenericTypeArguments;
		var convertFrom = args.FirstOrDefault();
		if (convertFrom == null)
		{
			Log.Warning($"Could not resolve converter type {converter.Name.ToString().Color(Color.Gold)}");
			return;
		}

		if (_converters.ContainsKey(convertFrom))
		{
			_converters.Remove(convertFrom);
			Log.Info($"Unregistered converter {converter.Name}");
		}
		else
		{
			Log.Warning($"Call to UnregisterConverter for a converter that was not registered. Type: {converter.Name.ToString().Color(Color.Gold)}");
		}
	}

	internal static bool IsGenericConverterContext(Type rootType) => rootType?.BaseType?.Name == typeof(CommandArgumentConverter<>).Name;
	internal static bool IsSpecificConverterContext(Type rootType) => rootType?.BaseType?.Name == typeof(CommandArgumentConverter<,>).Name;

	public static void RegisterConverter(Type converter)
	{
		// check base type
		var isGenericContext = IsGenericConverterContext(converter);
		var isSpecificContext = IsSpecificConverterContext(converter);

		if (!isGenericContext && !isSpecificContext)
		{
			return;
		}

		Log.Debug($"Trying to process {converter} as specifc={isSpecificContext} generic={isGenericContext}");

		object converterInstance = Activator.CreateInstance(converter);
		MethodInfo methodInfo = converter.GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
		if (methodInfo == null)
		{
			// can't bud
			Log.Error("Can't find TryParse that matches");
			return;
		}

		var args = converter.BaseType.GenericTypeArguments;
		var convertFrom = args.FirstOrDefault();
		if (convertFrom == null)
		{
			Log.Error("Can't determine generic base type to convert from. ");
			return;
		}

		Type contextType = typeof(ICommandContext);
		if (isSpecificContext)
		{
			if (args.Length != 2 || !typeof(ICommandContext).IsAssignableFrom(args[1]))
			{
				Log.Error("Can't determine generic base type to convert from.");
				return;
			}

			contextType = args[1];
		}


		_converters.Add(convertFrom, (converterInstance, methodInfo, contextType));
	}

	public static void RegisterAll() => RegisterAll(Assembly.GetCallingAssembly());

	public static void RegisterAll(Assembly assembly)
	{
		var types = assembly.GetTypes();

		// Register Converters first as typically commands will depend on them.
		foreach (var type in types)
		{
			RegisterConverter(type);
		}

		foreach (var type in types)
		{
			RegisterCommandType(type);
		}
	}

	public static void RegisterCommandType(Type type)
	{
		var groupAttr = type.GetCustomAttribute<CommandGroupAttribute>();
		var assembly = type.Assembly;
		if (groupAttr != null)
		{
			// handle groups - IDK later
		}

		var methods = type.GetMethods();

		ConstructorInfo contextConstructor = type.GetConstructors()
			.Where(c => c.GetParameters().Length == 1 && typeof(ICommandContext).IsAssignableFrom(c.GetParameters().SingleOrDefault()?.ParameterType))
			.FirstOrDefault();

		foreach (var method in methods)
		{
			RegisterMethod(assembly, groupAttr, contextConstructor, method);
		}
	}

	private static void RegisterMethod(Assembly assembly, CommandGroupAttribute groupAttr, ConstructorInfo customConstructor, MethodInfo method)
	{
		var commandAttr = method.GetCustomAttribute<CommandAttribute>();
		if (commandAttr == null) return;

		// check for CommandContext as first argument to method
		var paramInfos = method.GetParameters();
		var first = paramInfos.FirstOrDefault();
		if (first == null || first.ParameterType is ICommandContext)
		{
			Log.Error($"Method {method.Name.ToString().Color(Color.Gold)} has no CommandContext as first argument");
			return;
		}

		var parameters = paramInfos.Skip(1).ToArray();

		var canConvert = parameters.All(param =>
		{
			if (param.Name == "_remainder" && param.ParameterType == typeof(string))
			{
				Log.Debug($"Method {method.Name.ToString().Color(Color.Gold)} has a _remainder parameter");
				return true;
			}

			if (_converters.ContainsKey(param.ParameterType))
			{
				Log.Debug($"Method {method.Name.ToString().Color(Color.Gold)} has a parameter of type {param.ParameterType.Name.ToString().Color(Color.Gold)} which is registered as a converter");
				return true;
			}

			var converter = TypeDescriptor.GetConverter(param.ParameterType);
			if (converter == null ||
				!converter.CanConvertFrom(typeof(string)))
			{
				Log.Warning($"Parameter {param.Name.ToString().Color(Color.Gold)} could not be converted, so {method.Name.ToString().Color(Color.Gold)} will be ignored.");
				return false;
			}

			return true;
		});

		if (!canConvert) return;

		var constructorType = customConstructor?.GetParameters().Single().ParameterType;

		var command = new CommandMetadata(commandAttr, assembly.GetName().Name, method, customConstructor, parameters, first.ParameterType, constructorType, groupAttr);

		// todo include prefix and group in here, this shoudl be a string match
		// todo handle collisons here

		// BAD CODE INC.. permute and cache keys -> command
		var groupNames = groupAttr == null ? new[] { "" } : groupAttr.ShortHand == null ? new[] { $"{groupAttr.Name} " } : new[] { $"{groupAttr.Name} ", $"{groupAttr.ShortHand} ", };
		var names = commandAttr.ShortHand == null ? new[] { commandAttr.Name } : new[] { commandAttr.Name, commandAttr.ShortHand };
		var prefix = DEFAULT_PREFIX; // TODO: get from attribute/config
		List<string> keys = new();
		foreach (var group in groupNames)
		{
			foreach (var name in names)
			{
				var key = $"{prefix}{group}{name}";
				_cache.AddCommand(key, parameters, command);
				keys.Add(key);
			}
		}

		var assemblyName = assembly.GetName().Name;
		AssemblyCommandMap.TryGetValue(assemblyName, out var commandKeyCache);
		commandKeyCache ??= new();
		commandKeyCache[command] = keys;
		AssemblyCommandMap[assemblyName] = commandKeyCache;
	}

	internal static Dictionary<string, Dictionary<CommandMetadata, List<string>>> AssemblyCommandMap { get; } = new();

	public static void UnregisterAssembly() => UnregisterAssembly(Assembly.GetCallingAssembly());

	public static void UnregisterAssembly(Assembly assembly)
	{
		var assemblyName = assembly.GetName().Name;
		
		foreach (var type in assembly.DefinedTypes)
		{
			_cache.RemoveCommandsFromType(type);
			UnregisterConverter(type);
			// TODO: There's a lot of nasty cases involving cross mod converters that need testing
			// as of right now the guidance should be to avoid depending on converters from a different mod
			// especially if you're hot reloading either.
		}

		AssemblyCommandMap.Remove(assemblyName);
	}
}
