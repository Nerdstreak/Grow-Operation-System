using GrowMcp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowMcp.Tests;

/// <summary>
/// Der Schlüssel für die Tür ins Heimnetz.
/// </summary>
/// <remarks>
/// Zwei Eigenschaften zählen: er überlebt einen Neustart — sonst müsste der
/// Betreiber seinen Klienten nach jedem Update neu einrichten — und er lässt sich
/// nicht Zeichen für Zeichen erraten.
/// </remarks>
public sealed class TokenSpeicherTests
{
    private static TokenSpeicher Neu() => new(NullLogger<TokenSpeicher>.Instance);

    [Fact]
    public void TheKeySurvivesARestart()
    {
        // Zwei Instanzen sind hier dasselbe wie zweimal Starten: gelesen wird aus
        // derselben Datei.
        Assert.Equal(Neu().Token, Neu().Token);
    }

    [Fact]
    public void TheRightKeyOpensAndAnyOtherDoesNot()
    {
        var speicher = Neu();

        Assert.True(speicher.Stimmt(speicher.Token));
        Assert.False(speicher.Stimmt(speicher.Token + "x"));
        Assert.False(speicher.Stimmt(speicher.Token[..^1]));
        Assert.False(speicher.Stimmt("falsch"));
        Assert.False(speicher.Stimmt(""));
        Assert.False(speicher.Stimmt(null));
    }

    [Fact]
    public void TheKeyIsLongEnoughToBeWorthNothingToAGuesser()
    {
        // 32 zufaellige Bytes, Base64 kodiert. Kuerzer waere Zierde statt Schutz.
        Assert.True(Neu().Token.Length >= 40);
    }

    [Fact]
    public void TheKeySurvivesACommandLine()
    {
        // Er steht in einem `claude mcp add ...`-Befehl in Anfuehrungszeichen.
        // Ein `/` oder `+` darin ist harmlos, ein `"` waere es nicht — und
        // Base64-URL erzeugt ohnehin keins davon.
        var token = Neu().Token;

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.DoesNotContain('"', token);
    }
}
