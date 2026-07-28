using System.Text.Json;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Slug ist die einzige Zutat für einen Weg aufs Handy, der hält.
/// </summary>
/// <remarks>
/// Der Ingress-Pfad trägt ein Token, das pro Anfrage wechselt — ein Lesezeichen
/// darauf stirbt. Stabil ist nur <c>/hassio/ingress/&lt;slug&gt;</c>. Raten geht
/// nicht: je nach Installationsweg heisst das Add-on <c>local_grow_os</c> oder
/// <c>&lt;repo-hash&gt;_grow_os</c>.
/// </remarks>
public sealed class SupervisorInfoServiceTests
{
    private static JsonDocument Json(string text) => JsonDocument.Parse(text);

    [Fact]
    public void ReadsTheSlugFromTheSupervisorEnvelope()
    {
        // Der Supervisor antwortet immer in { result, data }.
        using var document = Json("""{"result":"ok","data":{"slug":"a0d7b954_grow_os","name":"Grow OS"}}""");

        Assert.Equal("a0d7b954_grow_os", SupervisorInfoService.ReadSlug(document));
    }

    [Fact]
    public void ReadsTheLocallyInstalledSlugToo()
    {
        using var document = Json("""{"result":"ok","data":{"slug":"local_grow_os"}}""");

        Assert.Equal("local_grow_os", SupervisorInfoService.ReadSlug(document));
    }

    [Theory]
    [InlineData("""{"result":"ok"}""")]
    [InlineData("""{"result":"ok","data":{}}""")]
    [InlineData("""{"result":"ok","data":{"slug":""}}""")]
    [InlineData("""{"result":"ok","data":{"slug":"   "}}""")]
    [InlineData("""{"result":"ok","data":{"slug":42}}""")]
    public void WithoutAUsableSlug_NothingIsClaimed(string body)
    {
        // Lieber gar kein Pfad als ein erfundener: ein QR-Code auf eine falsche
        // Adresse fällt erst auf, wenn jemand mit dem Handy davorsteht.
        using var document = Json(body);

        Assert.Null(SupervisorInfoService.ReadSlug(document));
    }

    [Fact]
    public void TheSlugBecomesThePanelPath()
    {
        Assert.Equal("/hassio/ingress/local_grow_os", SupervisorInfoService.PanelPath("local_grow_os"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void WithoutASlug_ThereIsNoPanelPath(string? slug)
    {
        Assert.Null(SupervisorInfoService.PanelPath(slug));
    }
}
