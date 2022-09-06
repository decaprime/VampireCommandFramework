using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;
using VampireCommandFramework.Basics;
using VampireCommandFramework.Registry;

namespace VCF.Tests;
public class HelpTests
{
	[Test]
	public void CanGenerateHelpText()
	{
		var (commandName, usage, description) = Any.ThreeStrings();

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: usage, description: description), null, null, null, null, null);
		var text = HelpCommands.GenerateHelpText(command);
		Assert.That(text, Is.EqualTo($"{commandName} {usage} (todo)"));
	}
}
