using Consumer;
using VampireCommandFramework;
using NUnit.Framework;
using Moq;
using System.Reflection;

namespace VCF.Tests
{
	public class MiddlewareTests
	{
		private ICommandContext TEST_CONTEXT;

		[SetUp]
		public void Setup()
		{
			Log.Instance = new BepInEx.Logging.ManualLogSource("Test");
			CommandRegistry.Reset();
			CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			TEST_CONTEXT = new Mock<ICommandContext>().Object;
		}

		[Test]
		public void CallsMiddlewareMethods()
		{
			Mock<CommandMiddleware> mockMiddleware = new();

			mockMiddleware.Setup(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Returns(true);

			CommandRegistry.Middlewares.Add(mockMiddleware.Object);
			Assert.IsNotNull(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"));


			mockMiddleware.Verify(m => m.BeforeExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware.Verify(m => m.AfterExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware.Verify(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware.VerifyNoOtherCalls();
		}

		[Test]
		public void CanExecuteBlocksExecution()
		{
			Mock<CommandMiddleware> mockMiddleware = new();

			mockMiddleware.Setup(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Returns(false);

			CommandRegistry.Middlewares.Add(mockMiddleware.Object);
			Assert.IsNull(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"));


			mockMiddleware.Verify(m => m.BeforeExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Never);
			mockMiddleware.Verify(m => m.AfterExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Never);
			mockMiddleware.Verify(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware.VerifyNoOtherCalls();
		}

		[Test] // todo: lol thanks copilot
		public void CanExecuteBlocksExecutionWithException()
		{
			Mock<CommandMiddleware> mockMiddleware = new();

			mockMiddleware.Setup(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Returns(true);
			mockMiddleware.Setup(m => m.BeforeExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Throws(new Exception());
		}

		[Test]
		public void MultipleMiddlewareAreCalledInOrder()
		{
			Mock<CommandMiddleware> mockMiddleware1 = new();
			Mock<CommandMiddleware> mockMiddleware2 = new();
			Mock<CommandMiddleware> mockMiddleware3 = new();

			mockMiddleware1.Setup(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Returns(true);
			mockMiddleware2.Setup(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>())).Returns(false);

			CommandRegistry.Middlewares.Add(mockMiddleware1.Object);
			CommandRegistry.Middlewares.Add(mockMiddleware2.Object);
			CommandRegistry.Middlewares.Add(mockMiddleware3.Object);


			Assert.IsNull(CommandRegistry.Handle(TEST_CONTEXT, ".horse breed"));


			mockMiddleware1.Verify(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware2.Verify(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Once);
			mockMiddleware3.Verify(m => m.CanExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Never);

			var allMocks = new List<Mock<CommandMiddleware>>() { mockMiddleware1, mockMiddleware2, mockMiddleware3 };
			allMocks.ForEach(m => m.Verify(m => m.BeforeExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Never));
			allMocks.ForEach(m => m.Verify(m => m.AfterExecute(It.IsAny<ICommandContext>(), It.IsAny<ChatCommandAttribute>(), It.IsAny<MethodInfo>()), Times.Never));
		}
	}
}