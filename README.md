# VampireCommandFramework
![](https://github.com/decaprime/VampireCommandFramework/raw/main/images/logo_128.png) 

A comprehensive framework for V Rising mod developers to easily build commands with advanced features like command overloading, history, smart matching, and plugin-specific execution.
## Usage

**For Server Operators**

This plugin should be installed into the `BepInEx/plugins` folder and be kept up to date. It is required by other plugins for commands.



<details><summary><strong>For Plugin Developers</strong></summary>

## How to use

#### 1. Add a reference to the plugin
>`dotnet add package VRising.VampireCommandFramework`

#### 2. Add the plugin as a dependency by setting this attribute on your plugin class
>`[BepInDependency("gg.deca.VampireCommandFramework")]`

#### 3. Register your plugin when you're done loading:
>`CommandRegistry.RegisterAll()`

#### 4. Write commands
```csharp
[Command("foo")]
public void Foo(ICommandContext ctx, int count, string orValues = "with defaults", float someFloat = 3f) 
    => ctx.Reply($"You'd do stuff here with your parsed {count} and stuff");
```

This command would execute for:
- `.foo 5`
- `.foo 5 works`
- `.foo 5 "or really fancy" 3.145`

But if you typed `.foo 5.123` you'd see a generated usage message like:
>
```*.foo (count) [orValues="with defaults"] [someFloat=3]*```

### This simple example provides
- **Automatic help listings** - your command appears in `.help`
- **Parameter parsing** - `count`, `orValues`, and `someFloat` are automatically converted
- **Usage text generation** - help shows `(count) [orValues="with defaults"] [someFloat=3]`
- **Default parameter handling** - optional parameters work seamlessly
- **Type conversion** - strings become integers, floats, etc.

### More Examples

The framework handles these additional command patterns:

**Command with parameters:**
```csharp
[Command("give")]
public void GiveItem(ICommandContext ctx, string item, int count = 1)
    => ctx.Reply($"Gave {count} {item}");
```
- Executes with: `.give sword`, `.give sword 5`

**Command groups:**
```csharp
[CommandGroup("admin")]
public class AdminCommands
{
    [Command("ban")]
    public void Ban(ICommandContext ctx, string player) 
        => ctx.Reply($"Banned {player}");
}
```
- Executes with: `.admin ban PlayerName`

## Command Overloading
You can now create multiple commands with the same name but different parameter types:

```csharp
[Command("teleport")]
public void TeleportToPlayer(ICommandContext ctx, string playerName) 
    => ctx.Reply($"Teleporting to {playerName}");

[Command("teleport")]
public void TeleportToCoords(ICommandContext ctx, float x, float y) 
    => ctx.Reply($"Teleporting to {x}, {y}");
```

When there's ambiguity, players will be presented with options and can select using `.1`, `.2`, etc.

## Middleware

All commands execute through a middleware pipeline. You can add your own middleware by implementing `ICommandMiddleware` and adding it to the `CommandRegistry.Middlewares` list. 

Middleware is perfect for implementing permissions and roles, cooldowns, logging, command costs, rate limiting, and other cross-cutting concerns that should apply across commands even from other VCF plugins.

 [V Roles](https://github.com/Odjit/VRoles) is an example of a Middleware plugin for VCF that adds in roles that commands and users can get assigned.

Example middleware:
```csharp
public class CooldownMiddleware : CommandMiddleware
{
    public override bool CanExecute(ICommandContext ctx, CommandAttribute cmd, MethodInfo method)
    {
        // Your cooldown logic here
        return !IsOnCooldown(ctx.Name, cmd.Name);
    }
}
```

## Response and Formatting Utilities

The framework includes rich formatting utilities for enhanced chat responses:

```csharp
// Text formatting
ctx.Reply($"{"Important".Bold()} message with {"emphasis".Italic()}");
ctx.Reply($"{"Warning".Underline()} - please read carefully");

// Colors (using predefined color constants)
ctx.Reply($"{"Error".Color(Color.Red)} - something went wrong");
ctx.Reply($"{"Success".Color(Color.Green)} - command completed");
ctx.Reply($"{"Info".Color(Color.LightBlue)} message");

// Text sizing
ctx.Reply($"{"Large Header".Large()} with {"small details".Small()}");

// Combining formatting
ctx.Reply($"{"Critical".Bold().Color(Color.Red).Large()} system alert!");

// Paginated replies for long content
var longText = "Very long text that might exceed chat limits...";
ctx.SysPaginatedReply(longText); // Automatically splits into multiple messages
```
<details>
<summary><strong>Available colors include: </strong></summary>
`Red`, `Primary`, `White`, `LightGrey`, `Yellow`, `Green`, `Command`, `Beige`, `Gold`, `Lavender`, `Pink`, `Periwinkle`, `Teal`, `LightRed`, `LightPurple`, `Lilac`, `LightPink`, `SoftBGrey`, `AdminUsername`, `ClanName`, `LightBlood`, `Blood`, `LightChaos`, `Chaos`, `LightUnholy`, `Unholy`, `LightIllusion`, `Illusion`, `LightFrost`, `Frost`, `LightStorm`, `Storm`, `Discord`, `Global`, and `ClassicUsername`.
</details>

## Custom Type Converters
Create converters for your custom types:

```csharp
public class PlayerConverter : CommandArgumentConverter<Player>
{
    public override Player Parse(ICommandContext ctx, string input)
    {
        var player = FindPlayer(input);
        if (player == null)
            throw ctx.Error($"Player '{input}' not found");
        return player;
    }
}
```
</details>

## Framework Features
The VampireCommandFramework also provides these powerful features across all commands:
- **Enhanced help system** with filtering and search
- **Command overloading** - multiple commands with same name but different parameters
- **Command history and recall** - players can repeat previous commands  
- **Intelligent command matching** - suggests closest matches for typos
- **Plugin-specific command execution** - target commands from specific mods
- **Case-insensitive commands** - works regardless of capitalization
- **Middleware pipeline** for permissions, cooldowns, etc.

### Enhanced Help System

The help system has been significantly improved:

- `.help` - Lists all available plugins
- `.help <plugin>` - Shows commands for a specific plugin
- `.help <command>` - Shows detailed help for a specific command
- `.help-all` - Shows all commands from all plugins  
- `.help-all <filter>` - Shows all commands matching the filter

All help commands support case-insensitive searching and include:
- Command descriptions and usage
- Parameter information with types and defaults
- Enum value listings
- Custom converter usage information

## Advanced Usage

### Command History and Recall
Players can easily repeat previous commands:
- `.!` - Repeat the last command
- `.! 3` - Repeat the 3rd most recent command  
- `.! list` or `.! l` - Show command history (last 10 commands)

### Smart Command Matching
When a command isn't found, the system suggests up to 3 closest matches:
```
Command not found: .tleport
Did you mean: .teleport, .teleport-home, .tp
```

### Plugin-Specific Commands
Players can execute commands from specific plugins to avoid conflicts:
```
.HealthMod heal
.MagicMod heal
```

### Command Overloading
Some commands can work with different types of input. For example, a teleport command might accept either a player name or coordinates:
```
.teleport PlayerName
.teleport 100 200
```
When your input could match multiple command variations, you'll see a list of options and can select the one you want using `.1`, `.2`, etc.



## Universal Configuration Management
Built-in commands for managing BepInEx configurations across all plugins:
- `.config dump <plugin>` - View plugin configuration
- `.config set <plugin> <section> <key> <value>` - Modify settings



## Help
Please feel free to direct questions to @decaprime or @odjit on discord at the [V Rising Modding Discord](https://vrisingmods.com/discord)