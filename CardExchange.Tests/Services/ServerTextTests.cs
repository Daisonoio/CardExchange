using System.Text;
using System.Text.Json;
using CardExchange.API.Localization;
using CardExchange.API.Services.Email;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CardExchange.Tests.Services;

public class ServerTextTests
{
    [Theory]
    [InlineData("Credenziali non valide", "en", "Invalid credentials")]
    [InlineData("Credenziali non valide", "it", "Credenziali non valide")]
    [InlineData("Credenziali non valide", "en-US,en;q=0.9", "Invalid credentials")]
    [InlineData("Credenziali non valide", "de", "Credenziali non valide")]
    [InlineData("Testo sconosciuto", "en", "Testo sconosciuto")]
    [InlineData("Hai raggiunto il limite di 20 messaggi giornalieri", "en", "You've reached the limit of 20 messages per day")]
    [InlineData("giulia ti ha inviato un'offerta di scambio", "en", "giulia sent you a trade offer")]
    [InlineData("Fiera di Milano · Stand 4 · fino alle 18:30", "en", "Fiera di Milano · Stand 4 · until 18:30")]
    [InlineData("CardExchange ti ha portato 3 persone in negozio. 2 scambi conclusi da te · 1 presenze ai tuoi eventi · 4 nuovi follower, 1 iscritti dal tuo QR · 30 visite alla scheda del negozio", "en",
        "CardExchange brought 3 people to your store. 2 trades completed at your store · 1 attendances at your events · 4 new followers, 1 sign-ups from your QR · 30 store page visits")]
    public void Translates(string text, string lang, string expected) => ServerText.T(text, lang).Should().Be(expected);

    [Fact]
    public void Dates_AreRewritten()
    {
        ServerText.T("marco propone di incontrarvi sabato 7 novembre alle 15:30 a Games Corner.", "en")
            .Should().Be("marco suggests meeting Saturday 7 November at 15:30 at Games Corner.");
        ServerText.T("Il tuo mese a novembre 2026: Games Corner", "en").Should().Be("Your month, November 2026: Games Corner");
    }

    [Fact]
    public void NestedValues_AreTranslated()
    {
        ServerText.T("3 tuoi invitati sono diventati attivi: ecco 2 mesi di Premium in regalo. Grazie per far crescere CardExchange nella tua zona.", "en")
            .Should().Be("3 of your invitees are now active: here's 2 months of Premium for free. Thanks for growing CardExchange in your area.");
    }

    [Fact]
    public void Email_InEnglish()
    {
        var m = EmailTemplates.ResetPassword("Ann", "https://x/reset", "en");
        m.Subject.Should().Be("Reset your password - CardExchange");
        m.HtmlBody.Should().Contain("<html lang=\"en\">").And.Contain("Hi Ann,").And.Contain("If the button doesn");
        EmailTemplates.ResetPassword("Anna", "https://x/reset").TextBody.Should().StartWith("Ciao Anna,");
    }

    [Fact]
    public async Task Middleware_TranslatesTextKeys_OnlyForEnglish()
    {
        async Task<string> Run(string lang, object payload)
        {
            var mw = new LocalizeResponsesMiddleware(async ctx =>
            {
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
            });
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/api/x";
            ctx.Request.Headers.AcceptLanguage = lang;
            var body = new MemoryStream();
            ctx.Response.Body = body;
            await mw.InvokeAsync(ctx);
            return Encoding.UTF8.GetString(body.ToArray());
        }

        var payload = new
        {
            message = "Utente non trovato",
            errors = new Dictionary<string, string[]> { ["Password"] = new[] { "La password è obbligatoria" } },
            rows = new[] { new { name = "Utente non trovato", warnings = new[] { "Carta gradata (PSA 10): importata come Mint" } } }
        };
        var en = await Run("en", payload);
        en.Should().Contain("\"message\":\"User not found\"").And.Contain("Password is required").And.Contain("Graded card (PSA 10): imported as Mint");
        en.Should().Contain("\"name\":\"Utente non trovato\"", "i campi che non sono testi del server restano invariati");
        (await Run("it", payload)).Should().Contain("Utente non trovato\",\"errors");
    }
}
