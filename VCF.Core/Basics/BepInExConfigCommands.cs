using BepInEx.IL2CPP;
using System;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Basics;

[CommandGroup("config")]
public class BepInExConfigCommands
{
	[Command("dump")]
	public void DumpConfig(ICommandContext ctx, string pluginGuid)
	{
		var (guid, info) = IL2CPPChainloader.Instance.Plugins.FirstOrDefault(x => x.Value.Metadata.GUID.Contains(pluginGuid, StringComparison.InvariantCultureIgnoreCase));
		if (info == null || info.Instance is not BasePlugin plugin)
		{
			foreach (var p in IL2CPPChainloader.Instance.Plugins)
			{
				ctx.SysReply($"Found: {p.Value.Metadata.GUID}");
			}

			throw ctx.Error("Can not find that plugin");
		}

		DumpConfig(ctx, guid, plugin);
	}

	[Command("set")]
	public void DumpConfig(ICommandContext ctx, string pluginGuid, string section, string key, string value)
	{
		var (guid, info) = IL2CPPChainloader.Instance.Plugins.FirstOrDefault(x => x.Value.Metadata.GUID.Contains(pluginGuid, StringComparison.InvariantCultureIgnoreCase));
		if (info == null || info.Instance is not BasePlugin plugin)
		{
			foreach (var p in IL2CPPChainloader.Instance.Plugins)
			{
				ctx.SysReply($"Found: {p.Value.Metadata.GUID}");
			}

			throw ctx.Error("Can not find that plugin");
		}

		var (def, entry) = plugin.Config.FirstOrDefault(k => k.Key.Section.Equals(section, StringComparison.InvariantCultureIgnoreCase) &&
										  k.Key.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
		if (def == null)
		{
			throw ctx.Error("Could not find property");
		}

		try
		{
			// this duplicates conversion but lets VCF catch the exception to reflect to user
			var convertedValue = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
			entry.SetSerializedValue(value);
			if (!plugin.Config.SaveOnConfigSet) plugin.Config.Save();

			ctx.SysReply($"Set {def.Key} = {convertedValue}");
		}
		catch (Exception e)
		{
			throw ctx.Error($"Can not convert {value} to {entry.SettingType}");
		}
	}

	private static void DumpConfig(ICommandContext ctx, string guid, BasePlugin plugin)
	{
		var cfg = plugin.Config;
		var sb = new StringBuilder();
		sb.AppendLine($"Path: " + cfg.ConfigFilePath);
		sb.AppendLine($"Dumping config for {guid.Color("#f0f")} with {cfg.Count()} entries");

		foreach (var section in cfg.GroupBy(k => k.Key.Section).OrderBy(k => k.Key))
		{
			sb.AppendLine($"[{section.Key}]");
			foreach (var (def, entry) in section)
			{
				sb.AppendLine($"{def.Key.Color(Color.White)} = {entry.BoxedValue.ToString().Color(Color.LightGrey)}");
			}
		}
		ctx.SysPaginatedReply(sb);
	}
}
