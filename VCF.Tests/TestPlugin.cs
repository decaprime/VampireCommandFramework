﻿using Unity.Entities;
using VampireCommandFramework;

namespace Consumer;

[ChatCommandGroup("horse", shortHand: "h")]
public class HorseCommands
{
	private Entity? ClosestHorse;
	public HorseCommands(CommandContext ctx)
	{
		ClosestHorse = HorseUtility.FindClosestHorse(ctx);
	}

	[ChatCommand("breed")]
	public void Breed(CommandContext ctx)
	{
		Console.WriteLine("I don't mean to stare, we don't have to breed.");
	}

	[ChatCommand("call")]
	public void Call(CommandContext ctx, NamedHorse? target = null)
	{
		Console.WriteLine($"You called? {(target == null ? "Default" : "Closest")}");
		var horse = target?.Horse ?? ClosestHorse!;
		/* ... */
	}
	
	[ChatCommand("call")]
	public void Call(CommandContext ctx)
	{
		// TODO: Should error since the above covers 0-1 parameters
	}
	
	[ChatCommand("call")]
	public void Call(CommandContext ctx, int count)
	{
		//TODO: Also should error since we can't decide if this is a horse or a count
		// TODO: Also vice-versa,
		/* ... */
	}

	[ChatCommand("set speed")]
	public void SetSpeed(CommandContext ctx, float newSpeed)
	{
		/* ... */
	}

	[ChatCommand("color")]
	public void ColorHorse(CommandContext ctx, HorseColor color)
	{
		/* ... */
	}

	[ChatCommand("color")] // < -- TODO: should error if we want parameter counting
	public void ColorHorse(CommandContext ctx, string color)
	{
		/* ... */
	}
}

public record NamedHorse(Entity Horse);
public enum HorseColor { Black, Brown, Blonde }

public class NamedHorseConverter : ChatCommandArgumentConverter<NamedHorse?>
{
	public override bool TryParse(CommandContext ctx, string input, out NamedHorse? result)
	{
		/* check some cache or perform entity query */  
		result = null;
		return (input == "Ted");
	}
}

internal class HorseUtility
{
	internal static Entity? FindClosestHorse(CommandContext ctx)
	{
		return null;
	}
}
