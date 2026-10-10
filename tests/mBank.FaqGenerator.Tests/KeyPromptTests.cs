using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

/// <summary>Reading the API key (FR-410–413).</summary>
public sealed class KeyPromptTests : IDisposable
{
    private const string Prompt = "Klucz API Azure OpenAI: ";

    private readonly FakeKeyInput keys = new();
    private readonly StringWriter stdout = new();

    public void Dispose() => stdout.Dispose();

    [Fact]
    public void Console_EveryCharacterEchoesOneStar_KeyNeverPrinted()
    {
        keys.Type("Ab-123-zażółć").Enter();

        string? key = KeyPrompt.Read(keys, stdout);

        Assert.Equal("Ab-123-zażółć", key);
        Assert.Equal(Prompt + new string('*', 13) + Environment.NewLine, stdout.ToString());
    }

    [Fact]
    public void Console_PastedSequence_StarPerCharacter()
    {
        string pasted = new('k', 84);
        keys.Type(pasted).Enter();

        Assert.Equal(pasted, KeyPrompt.Read(keys, stdout));
        Assert.Equal(84, stdout.ToString().Count(c => c == '*'));
    }

    [Fact]
    public void Console_BackspaceRemovesLastCharacter()
    {
        keys.Press(ConsoleKey.Backspace, '\b').Type("abc").Press(ConsoleKey.Backspace, '\b').Type("d").Enter();

        Assert.Equal("abd", KeyPrompt.Read(keys, stdout));
        Assert.Equal(Prompt + "***\b \b*" + Environment.NewLine, stdout.ToString());
    }

    [Fact]
    public void Console_SurroundingWhitespaceTrimmed()
    {
        keys.Type("  klucz  ").Enter();

        Assert.Equal("klucz", KeyPrompt.Read(keys, stdout));
    }

    [Fact]
    public void Console_EmptyKey_IsAskedAgain()
    {
        keys.Enter().Type("   ").Enter().Type("klucz").Enter();

        Assert.Equal("klucz", KeyPrompt.Read(keys, stdout));
        string output = stdout.ToString();
        Assert.Equal(2, output.Split("Klucz nie może być pusty.").Length - 1);
        Assert.Equal(3, output.Split(Prompt).Length - 1);
    }

    [Fact]
    public void Console_ArrowsTabAndEscapeIgnored()
    {
        keys.Type("a")
            .Press(ConsoleKey.LeftArrow)
            .Press(ConsoleKey.UpArrow)
            .Press(ConsoleKey.Tab, '\t')
            .Press(ConsoleKey.Escape, '\u001b')
            .Type("b")
            .Enter();

        Assert.Equal("ab", KeyPrompt.Read(keys, stdout));
        Assert.Equal(Prompt + "**" + Environment.NewLine, stdout.ToString());
    }

    [Fact]
    public void Console_CtrlC_Cancels_AndReleasesCapture()
    {
        keys.Type("abc").Press(ConsoleKey.C, '\u0003', control: true);

        Assert.ThrowsAny<OperationCanceledException>(() => KeyPrompt.Read(keys, stdout));
        Assert.Equal(1, keys.Captures);
        Assert.False(keys.Capturing);
    }

    [Fact]
    public void Console_CaptureReleasedAfterSuccess()
    {
        keys.Type("klucz").Enter();

        KeyPrompt.Read(keys, stdout);

        Assert.Equal(1, keys.Captures);
        Assert.False(keys.Capturing);
    }
}
