using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Registry;

public static class CommandHistory
{
    #region Private Fields
    
    private static Dictionary<string, List<(string input, CommandMetadata Command, object[] Args)>> _commandHistory = new();
    private const int MAX_COMMAND_HISTORY = 10; // Store up to 10 past commands
    
    // Track which users have had their history loaded this session
    private static HashSet<string> _loadedHistories = new();
    
    // Command history directory path
    private static string HistoryDirectory => Path.Combine(Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME), "CommandHistory");
    
    #endregion

    #region Public Methods
    
    internal static void Reset()
    {
        _commandHistory.Clear();
        _loadedHistories.Clear();
    }

	internal static bool IsHistoryLoaded(string contextName)
    {
        return _loadedHistories.Contains(contextName);
    }

	internal static void EnsureHistoryLoaded(ICommandContext ctx)
    {
        var contextName = ctx.Name;
        if (!_loadedHistories.Contains(contextName))
        {
            LoadHistoryFromFile(ctx, contextName);
        }
    }

	internal static void AddToHistory(ICommandContext ctx, string input, CommandMetadata command, object[] args)
    {
        var contextName = ctx.Name;

        // Create the history list for this context if it doesn't exist yet
        if (!_commandHistory.TryGetValue(contextName, out var history))
        {
            history = new List<(string input, CommandMetadata Command, object[] Args)>();
            _commandHistory[contextName] = history;
        }

        // Check if this exact command with same arguments already exists in history
        for (int i = 0; i < history.Count; i++)
        {
            var historyEntry = history[i];

            // Skip entries that couldn't be parsed at load
            if (historyEntry.Command == null) continue;
            
            // Compare command metadata (same command method) and arguments
            if (historyEntry.Command.Method == command.Method && 
                historyEntry.Command.Attribute.Name == command.Attribute.Name &&
                ArgsEqual(historyEntry.Args, args))
            {
                // Remove the existing duplicate
                history.RemoveAt(i);
                break; // Only remove the first match found as it should be the only one
            }
        }

        // Add the new command to the beginning of the list
        history.Insert(0, (input, command, args));

        // Keep only the most recent MAX_COMMAND_HISTORY commands
        if (history.Count > MAX_COMMAND_HISTORY)
        {
            history.RemoveAt(history.Count - 1);
        }

        // Save the updated history to file
        SaveHistoryToFile(contextName, history);
    }

	internal static CommandResult HandleHistoryCommand(ICommandContext ctx, string input, Func<ICommandContext, string, CommandResult> handleCommand, Func<ICommandContext, CommandMetadata, object[], CommandResult> executeCommandWithArgs)
    {
        var contextName = ctx.Name;

        // Remove the ".!" prefix
        string command = input.Substring(2).Trim();

        // Check if the command history exists for this context
        if (!_commandHistory.TryGetValue(contextName, out var history) || history.Count == 0)
        {
            ctx.SysReply($"{"[error]".Color(Color.Red)} No command history available.");
            return CommandResult.CommandError;
        }

        // Handle .! list or .! l commands
        if (command == "list" || command == "l")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Command history:");

            for (int i = 0; i < history.Count; i++)
            {
                sb.AppendLine($"{(i + 1).ToString().Color(Color.Gold)}. {history[i].input.Color(Color.Command)}");
            }

            ctx.SysPaginatedReply(sb);
            return CommandResult.Success;
        }

        // Handle .! # to execute a specific command by number
        if (int.TryParse(command, out int index) && index > 0 && index <= history.Count)
        {
            var selectedCommand = history[index - 1];
            ctx.SysReply($"Executing command {index.ToString().Color(Color.Gold)}: {selectedCommand.input.Color(Color.Command)}");
            
            // If Command and Args are available (successfully parsed), use them directly
            if (selectedCommand.Command != null && selectedCommand.Args != null)
            {
                return executeCommandWithArgs(ctx, selectedCommand.Command, selectedCommand.Args);
            }
            else
            {
                // Fall back to re-parsing if command wasn't successfully parsed during load
                return handleCommand(ctx, selectedCommand.input);
            }
        }

        // If just .! is provided, execute the most recent command
        if (string.IsNullOrWhiteSpace(command))
        {
            var mostRecent = history[0];
            ctx.SysReply($"Repeating most recent command: {mostRecent.input.Color(Color.Command)}");
            
            // If Command and Args are available (successfully parsed), use them directly
            if (mostRecent.Command != null && mostRecent.Args != null)
            {
                return executeCommandWithArgs(ctx, mostRecent.Command, mostRecent.Args);
            }
            else
            {
                // Fall back to re-parsing if command wasn't successfully parsed during load
                return handleCommand(ctx, mostRecent.input);
            }
        }

        // Invalid command
        ctx.SysReply($"{"[error]".Color(Color.Red)} Invalid command history selection. Use {".! list".Color(Color.Command)} to see available commands or {".! #".Color(Color.Command)} to execute a specific command.");
        return CommandResult.UsageError;
    }
    
    #endregion

    #region Private Helper Methods
    
    private static bool ArgsEqual(object[] args1, object[] args2)
    {
        if (args1 == null && args2 == null) return true;
        if (args1 == null || args2 == null) return false;
        if (args1.Length != args2.Length) return false;

        // Skipping the first index as that is always the context
        for (int i = 1; i < args1.Length; i++)
        {
            if (!Equals(args1[i], args2[i]))
            {
                return false;
            }
        }

        return true;
    }
    
    private static void SaveHistoryToFile(string contextName, List<(string input, CommandMetadata Command, object[] Args)> history)
    {
        try
        {
            if (!Directory.Exists(HistoryDirectory))
            {
                Directory.CreateDirectory(HistoryDirectory);
            }

            // Use a safe filename by replacing invalid characters
            var safeFileName = string.Join("_", contextName.Split(Path.GetInvalidFileNameChars()));
            string filePath = Path.Combine(HistoryDirectory, $"{safeFileName}.txt");
            var inputsOnly = history.Select(h => h.input).ToArray();
            File.WriteAllLines(filePath, inputsOnly);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to save command history for context {contextName}: {ex.Message}");
        }
    }

    private static void LoadHistoryFromFile(ICommandContext ctx, string contextName)
    {
        try
        {
            // Use a safe filename by replacing invalid characters
            var safeFileName = string.Join("_", contextName.Split(Path.GetInvalidFileNameChars()));
            string filePath = Path.Combine(HistoryDirectory, $"{safeFileName}.txt");

            if (!File.Exists(filePath))
            {
                return; // No history file exists
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
            {
                return; // Empty file
            }

            var reconstructedHistory = new List<(string input, CommandMetadata Command, object[] Args)>();

            // Process lines in reverse to maintain chronological order (most recent first)
            lines.Reverse();
            foreach (var input in lines)
            {
                try
                {
                    // Parse the command to get CommandMetadata and Args
                    var (command, args) = ParseCommandForHistory(ctx, input);
                    if (command != null && args != null)
                    {
                        reconstructedHistory.Add((input, command, args));
                    }
                    else
                    {
                        // If parsing fails, still add the input for display purposes
                        reconstructedHistory.Add((input, null, null));
                    }
                }
                catch (Exception)
                {
                    // If individual command parsing fails, add with null values
                    reconstructedHistory.Add((input, null, null));
                }
            }

            _commandHistory[contextName] = reconstructedHistory;
            _loadedHistories.Add(contextName);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load command history for context {contextName}: {ex.Message}");
            _loadedHistories.Add(contextName); // Mark as loaded to avoid repeated attempts
        }
    }

    private static (CommandMetadata command, object[] args) ParseCommandForHistory(ICommandContext ctx, string input)
    {
        try
        {
            // Ensure the command starts with the prefix
            if (!input.StartsWith(CommandRegistry.DEFAULT_PREFIX))
            {
                return (null, null);
            }

            // Remove the prefix for processing
            string afterPrefix = input.Substring(CommandRegistry.DEFAULT_PREFIX.Length);

            // Check if this could be an assembly-specific command
            string assemblyName = null;
            string commandInput = input;

            int spaceIndex = afterPrefix.IndexOf(' ');
            if (spaceIndex > 0)
            {
                string potentialAssemblyName = afterPrefix.Substring(0, spaceIndex);

                // Check if this could be a valid assembly name
                bool isValidAssembly = CommandRegistry.AssemblyCommandMap.Keys.Any(assemblyName =>
                    assemblyName.Equals(potentialAssemblyName, StringComparison.OrdinalIgnoreCase));

                if (isValidAssembly)
                {
                    assemblyName = potentialAssemblyName;
                    commandInput = "." + afterPrefix.Substring(spaceIndex + 1);
                }
            }

            // Get command(s) based on input - we need to access the cache through CommandRegistry
            var matchedCommand = CommandRegistry.GetCommandFromCache(commandInput, assemblyName);
            
            if (matchedCommand == null || !matchedCommand.IsMatched)
            {
                matchedCommand = CommandRegistry.GetCommandFromCache(input);
            }

            var commands = matchedCommand.Commands;

            if (!matchedCommand.IsMatched || !commands.Any())
            {
                return (null, null);
            }

            // Try to find the first command that can be parsed successfully
            foreach (var (command, cmdArgs) in commands)
            {
                if (!CommandRegistry.CanCommandExecute(ctx, command)) continue;

                var (success, commandArgs, error) = CommandRegistry.TryConvertParameters(ctx, command, cmdArgs, input);
                if (success)
                {
                    return (command, commandArgs);
                }
            }

            return (null, null);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }
    
    #endregion
}
