> # RFC: VampireCommandFramework 
> A framework for adding commands to VRising mods.
>
> ##### Updated `2022-07-16`
> ##### Status `WIP`

#

**VampireCommandFramework** is a library for adding commands to VRising mods focused on developer agility. It operates as a plugin dependency and handles chat parsing and command execution.

## Background

Currently all VRising plugins that add chat commands also add chat command infrastructure. They all implement a chat hook, some type of parsing, and command selection. The most robust public implementation was [ChatCommands](https://github.com/NopeyBoi/ChatCommands) which along with adding many commands, had an easy to understand mechanism for adding commands. This lead to a number of forks primarily for the sake of adding commands. [RPGMods](https://github.com/Kaltharos/RPGMods) was forked to add RPG features, but also needed a number of commands and now has the responsibility of maintaining all of them. The current common implementation expects to be all commands from all plugins with that prefix. 

My mod [LeadAHorseToWater](https://https://github.com/decaprime/LeadAHorseToWater) has no commands currently because I had no easy way to add them without duplicating effort. I only want to put horse things in this mod, so I'm not going to add either all of the command infrastructure nor all of the existing commands to it.

## Proposal

Build a framework that is isolated from command implementations. The framework should be responsible for handling the chat event for all dependent mods, selecting, and invoking commands. The framework should be flexible enough to replicate any of the existing command handling mechanisms.

Consumers of the framework will create methods with command implementations that they attribute with `ChatCommand`, these methods take in `ChatContext` to operate on along with whatever typed parameters they want to capture the arguments in their command. They then just have to register the assembly with VCF and it will handle calling those methods.  

### Middleware
To extend the basic functionality you can build middleware that operates before/after invoking a matched command. For example you could add cooldown functionality by creating a middleware that had a map of (user,command)->timestamp, in AfterExecute you capture the next valid time, in BeforeExecute you check againts the map. You can then stack this in some order with your other middleware to build whatever experience you'd like.

## Usage
#### Parameterless Command
```csharp
[ChatCommand("ping")]
public void FooCommand(CommandContext ctx) => ctx.Reply("pong");
```
### Types of Parameter Support
#### Command with primitive parameters
```csharp
[ChatCommand("math")]
public void MathCommand(CommandContext ctx, int a, float b) => ctx.Reply($"a > b: {a > b}");
```

#### Enums
```csharp
public enum AnimalTeam {Snakes, Monkeys, Pigs}

[ChatCommand("join")]
public void FarmCommand(CommandContext ctx, AnimalTeam team) => ctx.Reply($"You joined the {team} team!");
```

#### Default parameters

```csharp
[ChatCommand("greet")]
public void GreetCommand(CommandContext ctx, string greet = "world") => ctx.Reply($"Hello {greet}!");
```

#### Custom parameter parsing

```csharp

// Build a converter that matches the input string,
public class NamedHorseConverter : ChatCommandArgumentConverter<NamedHorse>
{
	public override bool TryParse(CommandContext ctx, string input, out NamedHorse result)
	{
		/* check some cache or perform entity query */  
        ...
		result = horseFound;    // set output in result
		return true;            // or false if no match
	}
}
```

```csharp
// Register the converter
CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
```

```csharp
// Use the type as a command parameter
[ChatCommand("racestats")]
public void RaceStats(CommandContext ctx, NamedHorse target) => ctx.Reply(target.Stats); 
```
