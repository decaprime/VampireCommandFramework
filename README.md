
# VampireCommandFramework
![](https://github.com/decaprime/VampireCommandFramework/raw/main/images/logo_128.png) Framework for V Rising mod developers to easily build commands.

## `BETA VERSION WARNING`
#### Please coordinate shipping plugins with this framework with me as I may still need to make breaking changes to the API.

Please feel free to ask any questions to [@deca#9999](https://discord.com/users/115195745782464512) on [V Rising Modding Discord](https://discord.gg/vrisingmods)

---

# For Server Operators

This plugin should be installed into the `BepInEx/plugins` folder and be kept up to date. It is required by other plugins for commands. In the future there will be more universal configurations for server operators to manage commands across plugins.

# For Plugin Developers
## How to use

### 1. Add a reference to the plugin
>`dotnet add package VRising.VampireCommandFramework`

### 2. Add the plugin as a dependency by setting this attribute on your plugin class
>`[BepInDependency("gg.deca.VampireCommandFramework")]`

### 3. Register your plugin when you're done loading:
>`CommandRegistry.RegisterAll()`

### 4. Write commands
```csharp
  [Command("foo")]
  public void Foo(ICommandContext ctx, int count, string orValues  = "with defaults", float someFloat=3f) 
                  => ctx.Reply($"You'd do stuff here with your parsed {count} and stuff");
```

### This gets you automatically
- Help command listings
- Argument usage text
- Command parsing
- Invoking your code with contexts

### For example
The above would execute for:
- `.foo 5`
- `.foo 5 works`
- `.foo 5 "or really fancy" 3.145`

But if you typed `.foo 5.123` you'd see a generated usage message back like 
>*.foo (count) [orValues ="with defaults"] [someFloat=3]*

## Middleware

All commands execute through the same pipeline and through a series of middleware. You can add your own middleware to the pipeline by adding a class that implements `ICommandMiddleware` and adding it to the `CommandRegistry.Middlewares` list. Middleware is where you'd implement things like cooldowns, permissions, logging, command 'costs', that could apply across commands even from other vcf plugins.

## Other features

- Custom type converters
- Context abstraction support for other types of commands (e.g. RCon, Console, UI, Discord, etc.)
- Response and formatting utilities
- Universal BepInEx config management commands for all (including non-vcf) plugins.
- Override config system for metadata on commands, this lets server operators do things like
  - Localization
  - Custom help text and descriptions
  - Disabling commands entirely