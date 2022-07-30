using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests
{
	public class GetPartsTests
	{
		[Test]
		public void GetParts_No_Quotes()
		{
			var empty = Array.Empty<string>();
			Assert.That(CommandRegistry.GetParts("blah blah"), Is.EqualTo(new[] { "blah", "blah" }));
			Assert.That(CommandRegistry.GetParts(""), Is.EqualTo(empty));
			Assert.That(CommandRegistry.GetParts(" "), Is.EqualTo(empty));
			Assert.That(CommandRegistry.GetParts("a "), Is.EqualTo(new[] { "a" }));
			Assert.That(CommandRegistry.GetParts(" a"), Is.EqualTo(new[] { "a" }));
			Assert.That(CommandRegistry.GetParts(" a  b    c "), Is.EqualTo(new[] { "a", "b", "c" }));
		}

		[Test]
		public void GetParts_Quotes()
		{
			Assert.That(CommandRegistry.GetParts("a \"b c\""), Is.EqualTo(new[] { "a", "b c" }));
			Assert.That(CommandRegistry.GetParts(" a  \" b    c \""), Is.EqualTo(new[] { "a", " b    c " }));
		}
	}
}
