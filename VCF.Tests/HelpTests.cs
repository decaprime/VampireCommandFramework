using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;
using VampireCommandFramework.Basics;
using VampireCommandFramework.Registry;

namespace VCF.Tests;
public class HelpTests
{	
	[Test]
	public void GenerateHelpText_UsageSpecified()
	{
		var (commandName, usage, description) = Any.ThreeStrings();

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: usage, description: description), null, null, null, null, null, null);
		var text = HelpCommands.GenerateHelpText(command);
		Assert.That(text, Is.EqualTo($"<color=#dd0>.</color><color=#eee>{commandName}</color> {usage}"));
	}

	[Test]
	public void GenerateHelpText_GeneratesUsage_NormalParam()
	{
		var (commandName, usage, description) = Any.ThreeStrings();
		var param = A.Fake<ParameterInfo>();
		var paramName = Any.String();
		A.CallTo(() => param.Name).Returns(paramName);

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: null, description: description), null, null, new[] { param }, null, null, null);

		var text = HelpCommands.GenerateHelpText(command);

		Assert.That(text, Is.EqualTo($"<color=#dd0>.</color><color=#eee>{commandName}</color> <color=#ccc>({paramName})</color>"));
	}

	[Test]
	public void GenerateHelpText_GeneratesUsage_DefaultParam()
	{
		var (commandName, usage, description) = Any.ThreeStrings();
		var (paramName, paramValue) = Any.TwoStrings(maxLength: 10);
		var param = A.Fake<ParameterInfo>();

		A.CallTo(() => param.Name).Returns(paramName);
		A.CallTo(() => param.DefaultValue).Returns(paramValue);
		A.CallTo(() => param.HasDefaultValue).Returns(true);

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: null, description: description), null, null, new[] { param }, null, null, null);

		var text = HelpCommands.GenerateHelpText(command);

		Assert.That(text, Is.EqualTo($"<color=#dd0>.</color><color=#eee>{commandName}</color> <color=#0c0>[{paramName}={paramValue}]</color>"));
	}
}