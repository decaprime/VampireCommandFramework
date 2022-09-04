using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class GetPartsTests
{
	[Test]
	public void GetParts_No_Quotes()
	{
		var empty = Array.Empty<string>();
		Assert.That(Utility.GetParts("blah blah"), Is.EqualTo(new[] { "blah", "blah" }));
		Assert.That(Utility.GetParts(""), Is.EqualTo(empty));
		Assert.That(Utility.GetParts(" "), Is.EqualTo(empty));
		Assert.That(Utility.GetParts("a "), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts(" a"), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts(" a  b    c "), Is.EqualTo(new[] { "a", "b", "c" }));
	}

	[Test]
	public void GetParts_Quotes()
	{
		Assert.That(Utility.GetParts("a \"b c\""), Is.EqualTo(new[] { "a", "b c" }));
		// TODO: Consider if this should be the result, it fails now
		//	Assert.That(CommandRegistry.GetParts(" a  \" b    c \""), Is.EqualTo(new[] { "a", " b    c " }));
	}
}
