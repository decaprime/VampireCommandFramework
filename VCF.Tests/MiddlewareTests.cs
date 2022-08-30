using Consumer;
using VampireCommandFramework;
using NUnit.Framework;
using FakeItEasy;
using System.Reflection;

namespace VCF.Tests;

public class MiddlewareTests
{
	private ICommandContext? TEST_CONTEXT;

	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		TEST_CONTEXT = A.Fake<ICommandContext>();
	}

	[Test]
	public void CallsMiddlewareMethods()
	{
		var middleware = A.Fake<CommandMiddleware>();

		A.CallTo(() => middleware.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Returns(true);

		CommandRegistry.Middlewares.Add(middleware);
		Assert.That(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"), Is.EqualTo(CommandResult.Success));


		A.CallTo(() => middleware.BeforeExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => middleware.AfterExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => middleware.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		Assert.That(Fake.GetCalls(middleware).Count(), Is.EqualTo(3), "No other calls were expected");
	}

	[Test]
	public void CanExecuteBlocksExecution()
	{
		var middleware = A.Fake<CommandMiddleware>();

		A.CallTo(() => middleware.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Returns(false);

		CommandRegistry.Middlewares.Add(middleware);
		Assert.That(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"), Is.EqualTo(CommandResult.Denied));


		A.CallTo(() => middleware.BeforeExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustNotHaveHappened();
		A.CallTo(() => middleware.AfterExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustNotHaveHappened();
		A.CallTo(() => middleware.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		Assert.That(Fake.GetCalls(middleware).Count(), Is.EqualTo(1), "No other calls were expected");
	}

	[Test] // todo: lol thanks copilot
	public void CanExecuteBlocksExecutionWithException()
	{
		var middleware = A.Fake<CommandMiddleware>();

		A.CallTo(() => middleware.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Returns(true);
		A.CallTo(() => middleware.BeforeExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Throws(new Exception());
	}

	[Test]
	public void MultipleMiddlewareAreCalledInOrder()
	{
		var middleware1 = A.Fake<CommandMiddleware>();
		var middleware2 = A.Fake<CommandMiddleware>();
		var middleware3 = A.Fake<CommandMiddleware>();

		A.CallTo(() => middleware2.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Returns(false);
		A.CallTo(() => middleware1.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).Returns(true);

		CommandRegistry.Middlewares.Add(middleware1);
		CommandRegistry.Middlewares.Add(middleware2);
		CommandRegistry.Middlewares.Add(middleware3);

		Assert.That(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"), Is.EqualTo(CommandResult.Denied));

		A.CallTo(() => middleware1.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => middleware2.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => middleware3.CanExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustNotHaveHappened();

		foreach (var mock in new[] { middleware1, middleware2, middleware3 })
		{
			A.CallTo(() => mock.BeforeExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustNotHaveHappened();
			A.CallTo(() => mock.AfterExecute(A<ICommandContext>._, A<CommandAttribute>._, A<MethodInfo>._)).MustNotHaveHappened();
		}
	}
}