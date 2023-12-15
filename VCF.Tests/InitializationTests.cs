using System.Reflection;
using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class InitializationTests
{
    [SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
	}

    [TearDown]
	public void TearDown()
	{
		CommandRegistry.Reset();
	}

    [Test]
    public void ScanningInterfaceDoesNotCauseException()
    {
        Assert.DoesNotThrow(ScanAssemblyContainingInterface);
    }

    private void ScanAssemblyContainingInterface()
    {
        CommandRegistry.RegisterAll(AssemblyContainingInterface());
    }

    private Assembly AssemblyContainingInterface()
    {
        var mockAssembly = A.Fake<Assembly>();
        A.CallTo(() => mockAssembly.GetTypes()).Returns(new Type[]{
            typeof(ISomeInterface),
        });
        return mockAssembly;
    }

    private interface ISomeInterface;

}