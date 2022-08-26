using Unity.Entities;
using VampireCommandFramework;

namespace Consumer;

[ChatCommandGroup("horse", shortHand: "h")]
public class HorseCommands
{
	private Entity? ClosestHorse;
	public HorseCommands(ICommandContext ctx)
	{
		ClosestHorse = HorseUtility.FindClosestHorse(ctx);
	}

	[ChatCommand("adminonly", adminOnly: true)]
	public void AdminCommand(ICommandContext ctx)
	{
		Console.WriteLine("You must be an admin");
	}

	[ChatCommand("breed")]
	public void Breed(ICommandContext ctx)
	{
		Console.WriteLine("I don't mean to stare, we don't have to breed.");
	}

	[ChatCommand("call")]
	public void Call(ICommandContext ctx, NamedHorse? target = null)
	{
		Console.WriteLine($"You called? {(target == null ? "Default" : "Closest")}");
		var horse = target?.Horse ?? ClosestHorse!;
		/* ... */
	}

	[ChatCommand("call")]
	public void Call(ICommandContext ctx, int a, int b)
	{
	}

	[ChatCommand("set speed")]
	public void SetSpeed(ICommandContext ctx, float newSpeed)
	{
		/* ... */
	}

	[ChatCommand("color")]
	public void ColorHorse(ICommandContext ctx, HorseColor color)
	{
		/* ... */
	}
}

public record NamedHorse(Entity Horse);
public enum HorseColor { Black, Brown, Blonde }

public class NamedHorseConverter : ChatCommandArgumentConverter<NamedHorse?>
{
	// Only Ted apparently
	public override NamedHorse? Parse(ICommandContext ctx, string input)
	{
		/* check some cache or perform entity query, null here to not pull in more */
		return (input == "Ted") ? null : throw ctx.Error("Only Ted");
	}
}

internal class HorseUtility
{
	internal static Entity? FindClosestHorse(ICommandContext ctx)
	{
		return null;
	}
}
