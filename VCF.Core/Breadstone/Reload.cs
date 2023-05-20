using BepInEx.Unity.IL2CPP;
using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using VampireCommandFramework.Common;
using System.IO;
using System.Reflection;

namespace VampireCommandFramework.Breadstone;

/// <summary>
/// This functionality is a server side adoption from Wetstone with VCF commands
/// </summary>
internal static class Reload
{
#nullable disable
	private static string _reloadPluginsFolder;
#nullable enable

	internal static List<BasePlugin> _loadedPlugins = new();

	internal static void Initialize(string reloadPluginsFolder)
	{
		_reloadPluginsFolder = reloadPluginsFolder;
		LoadPlugins();
	}

	[Command("reload","re", adminOnly:true)]
	public static void HandleReloadCommand(ChatCommandContext ctx)
	{
		UnloadPlugins();
		var loaded = LoadPlugins();

		if (loaded.Count > 0)
		{
			ctx.SysReply($"Reloaded {string.Join(", ", loaded)}. See console for details.");
		}
		else
		{
			ctx.SysReply($"Did not reload any plugins because no reloadable plugins were found. Check the console for more details.");
		}
	}

	private static void UnloadPlugins()
	{
		for (int i = _loadedPlugins.Count - 1; i >= 0; i--)
		{
			var plugin = _loadedPlugins[i];

			if (!plugin.Unload())
			{
				Log.Warning($"Plugin {plugin.GetType().FullName} does not support unloading, skipping...");
			}
			else
			{
				_loadedPlugins.RemoveAt(i);
			}
		}
	}

	private static List<string> LoadPlugins()
	{
		if (!Directory.Exists(_reloadPluginsFolder)) return new();

		return Directory.GetFiles(_reloadPluginsFolder, "*.dll").SelectMany(LoadPlugin).ToList();
	}

	private static List<string> LoadPlugin(string path)
	{
		var defaultResolver = new DefaultAssemblyResolver();
		defaultResolver.AddSearchDirectory(_reloadPluginsFolder);
		defaultResolver.AddSearchDirectory(Paths.ManagedPath);
		defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

		// Il2CppInteropManager.IL2CPPInteropAssemblyPath is internal so this will have to do
		defaultResolver.AddSearchDirectory(Path.Combine(Paths.BepInExRootPath, "interop"));

		using var dll = AssemblyDefinition.ReadAssembly(path, new() { AssemblyResolver = defaultResolver });
		dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

		using var ms = new MemoryStream();
		dll.Write(ms);

		var loaded = new List<string>();

		var assembly = Assembly.Load(ms.ToArray());
		foreach (var pluginType in assembly.GetTypes().Where(x => typeof(BasePlugin).IsAssignableFrom(x)))
		{
			// skip plugins not marked as reloadable
			if (!pluginType.GetCustomAttributes<ReloadableAttribute>().Any())
			{
				Log.Warning($"Plugin {pluginType.FullName} is not marked as reloadable, skipping...");
				continue;
			}

			// skip plugins already loaded
			if (_loadedPlugins.Any(x => x.GetType() == pluginType)) continue;

			try
			{
				// we skip chainloader here and don't check dependencies. Fast n dirty.
				var plugin = (BasePlugin)Activator.CreateInstance(pluginType);
				var metadata = MetadataHelper.GetMetadata(plugin);
				_loadedPlugins.Add(plugin);
				plugin.Load();
				loaded.Add(metadata.Name);
				
				Log.Info($"Loaded plugin {pluginType.FullName}");
			}
			catch (Exception ex)
			{
				Log.Error($"Plugin {pluginType.FullName} threw an exception during initialization:");
				Log.Error(ex.ToString());
			}
		}

		return loaded;
	}
}