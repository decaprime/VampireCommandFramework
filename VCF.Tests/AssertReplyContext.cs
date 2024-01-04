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
		Assert.That(RepliedTextLfAndTrimmed(), Is.EqualTo(expected));
	}
	public void AssertReplyContains(string expected)
	{
		var repliedText = RepliedTextLfAndTrimmed();
		Assert.That(repliedText.Contains(expected), Is.True, $"Expected {expected} to be contained in replied: {repliedText}");
	}

	public void AssertInternalError()
	{
		Assert.That(RepliedTextLfAndTrimmed(), Is.EqualTo("[vcf] An internal error has occurred."));
	}

	private string RepliedTextLfAndTrimmed()
	{
		return _sb.ToString()
			.Replace("\r\n", "\n") // LF instead of CRLF for line endings
			.TrimEnd(Environment.NewLine.ToCharArray());
	}
}
