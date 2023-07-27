using NUnit.Framework;
using System.Text;
using VampireCommandFramework;

namespace VCF.Tests;

public class AssertReplyContext : ICommandContext
{
	private StringBuilder _sb = new();
	public IServiceProvider Services => throw new NotImplementedException();

	public string Name { get; set; } = nameof(AssertReplyContext);

	public bool IsAdmin { get; set; }

	public CommandException Error(string LogMessage)
	{
		throw new CommandException(LogMessage);
	}

	public void Reply(string v)
	{
		_sb.AppendLine(v);
	}

	public void AssertReply(string expected)
	{
		Assert.That(_sb.ToString().TrimEnd(Environment.NewLine.ToCharArray()), Is.EqualTo(expected));
	}
	public void AssertReplyContains(string expected)
	{
		var repliedText = _sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
		Assert.That(repliedText.Contains(expected), Is.True, $"Expected {expected} to be contained in replied: {repliedText}");
	}

	public void AssertInternalError()
	{
		Assert.That(_sb.ToString().TrimEnd(Environment.NewLine.ToCharArray()), Is.EqualTo("[vcf] An internal error has occurred."));
	}
}
