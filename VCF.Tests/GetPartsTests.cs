using NUnit.Framework;
using VampireCommandFramework.Common;

namespace VCF.Tests;

public class GetPartsTests
{
	[Test]
	public void GetParts_Empty()
	{
		var empty = Array.Empty<string>();
		Assert.That(Utility.GetParts(""), Is.EqualTo(empty));
		Assert.That(Utility.GetParts(" "), Is.EqualTo(empty));
		Assert.That(Utility.GetParts(null), Is.EqualTo(empty));
	}

	[Test]
	public void GetParts_No_Quotes()
	{
		Assert.That(Utility.GetParts("blah blah"), Is.EqualTo(new[] { "blah", "blah" }));
		Assert.That(Utility.GetParts("a "), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts(" a"), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts(" a  b    c "), Is.EqualTo(new[] { "a", "b", "c" }));
	}

	[Test]
	public void GetParts_Quotes_Preserves_Spacing()
	{
		Assert.That(Utility.GetParts(" a  \" b    c \""), Is.EqualTo(new[] { "a", " b    c " }));
	}

	[Test]
	public void GetParts_Quotes_Many()
	{
		Assert.That(Utility.GetParts(" \"a\" \" b    c \" not  quoted "), Is.EqualTo(new[] { "a", " b    c ", "not", "quoted" }));
	}

	[Test]
	public void GetParts_Quote_SingleTerm()
	{
		Assert.That(Utility.GetParts("a"), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts("\"a\""), Is.EqualTo(new[] { "a" }));
		Assert.That(Utility.GetParts("\"abc\""), Is.EqualTo(new[] { "abc" }));
	}

	[Test]
	public void GetParts_Quote_SingleTerm_Positional()
	{
		Assert.That(Utility.GetParts("a \"b\" c"), Is.EqualTo(new[] { "a", "b", "c" }));
		Assert.That(Utility.GetParts("\"a\" b c"), Is.EqualTo(new[] { "a", "b", "c" }));
		Assert.That(Utility.GetParts("a b \"c\""), Is.EqualTo(new[] { "a", "b", "c" }));
	}

	[Test]
	public void GetParts_Escape_Quotes()
	{
		Assert.That(Utility.GetParts("\"I'm like \\\"O'rly?\\\", they're like \\\"ya rly\\\"\""), Is.EqualTo(new[] { "I'm like \"O'rly?\", they're like \"ya rly\"" }));
		Assert.That(Utility.GetParts(@"a\b\\""c\"""), Is.EqualTo(new[] { @"a\b\""c""" }));
	}

	[Test]
	public void GetParts_Escape_Slashliteral()
	{
		Assert.That(Utility.GetParts(@"\"), Is.EqualTo(new[] { @"\" }));
		Assert.That(Utility.GetParts(@"\\\"), Is.EqualTo(new[] { @"\\\" }));

		Assert.That(Utility.GetParts("\\a"), Is.EqualTo(new[] { "\\a" }));
		Assert.That(Utility.GetParts("\\\""), Is.EqualTo(new[] { "\"" }));
	}

	[Test]
	public void GetParts_Escape_Grouping()
	{	
		Assert.That(Utility.GetParts(@"\ \"), Is.EqualTo(new[] { @"\", @"\" }));
		Assert.That(Utility.GetParts(@"a\b  \  c"), Is.EqualTo(new[] { @"a\b", @"\", "c" }));
	}
}
