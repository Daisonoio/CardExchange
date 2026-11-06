using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class CardFingerprintTests
{
    /// <summary>Immagine sintetica 252 × 352: blocchi di colore diversi per seme, con una luminosità regolabile.</summary>
    private static byte[] Synthetic(int seed, double light = 1, int shiftX = 0)
    {
        var rnd = new Random(seed);
        var blocks = Enumerable.Range(0, 48).Select(_ => (r: rnd.Next(256), g: rnd.Next(256), b: rnd.Next(256))).ToArray();
        var px = new byte[CardFingerprint.InputWidth * CardFingerprint.InputHeight * 4];
        for (var y = 0; y < CardFingerprint.InputHeight; y++)
        for (var x = 0; x < CardFingerprint.InputWidth; x++)
        {
            var sx = Math.Clamp(x + shiftX, 0, CardFingerprint.InputWidth - 1);
            var (r, g, b) = blocks[(y * 8 / CardFingerprint.InputHeight) * 6 + sx * 6 / CardFingerprint.InputWidth];
            var i = (y * CardFingerprint.InputWidth + x) * 4;
            px[i] = (byte)Math.Clamp(r * light, 0, 255);
            px[i + 1] = (byte)Math.Clamp(g * light, 0, 255);
            px[i + 2] = (byte)Math.Clamp(b * light, 0, 255);
            px[i + 3] = 255;
        }
        return px;
    }

    [Fact]
    public void Fingerprint_HasFixedLength_AndIsDeterministic()
    {
        var a = CardFingerprint.FromRgba(Synthetic(1));
        a.Should().HaveCount(CardFingerprint.Length).And.HaveCount(932);
        CardFingerprint.FromRgba(Synthetic(1)).Should().Equal(a);
        CardFingerprint.Similarity(a, a).Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void SameCard_DifferentLightAndSmallShift_StaysCloserThanAnotherCard()
    {
        var card = CardFingerprint.FromRgba(Synthetic(1));
        var darker = CardFingerprint.FromRgba(Synthetic(1, light: 0.7, shiftX: 4));
        var other = CardFingerprint.FromRgba(Synthetic(2));

        CardFingerprint.Similarity(card, darker).Should().BeGreaterThan(0.9);
        CardFingerprint.Similarity(card, other).Should().BeLessThan(0.5);
    }

    [Fact]
    public void Bytes_RoundTrip()
    {
        var v = CardFingerprint.FromRgba(Synthetic(3));
        CardFingerprint.FromBytes(CardFingerprint.ToBytes(v)).Should().Equal(v);
    }

    [Fact]
    public void WrongInputSize_IsRejected()
    {
        ((Action)(() => CardFingerprint.FromRgba(new byte[10]))).Should().Throw<ArgumentException>();
    }
}
