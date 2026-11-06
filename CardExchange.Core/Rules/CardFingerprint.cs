namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Impronta visiva di una carta, per riconoscerla da una foto.
    /// Lo stesso calcolo esiste nel frontend (src/utils/cardFingerprint.ts): le due versioni devono restare identiche.
    ///
    /// 1. L'immagine della carta (sfondo trasparente su bianco) viene ridotta a 252 × 352 dalla piattaforma.
    /// 2. Media 2 × 2 → 126 × 176; si scarta un bordo del 5% (dove finiscono sfondo e disallineamenti).
    /// 3. Griglia 12 × 16 di medie RGB: colori normalizzati per canale (insensibili a luce e bilanciamento del bianco).
    /// 4. Differenze di luminosità tra celle vicine (contorni), normalizzate.
    /// 5. Vettore di 932 valori, lunghezza 1, salvato come byte con segno (× 512).
    /// </summary>
    public static class CardFingerprint
    {
        public const int Version = 1;
        public const int InputWidth = 252, InputHeight = 352;
        public const int BaseWidth = 126, BaseHeight = 176;
        public const int Cols = 12, Rows = 16;
        public const int Length = Cols * Rows * 3 + Rows * (Cols - 1) + (Rows - 1) * Cols; // 932
        private const double Inset = 0.05;
        private const double Scale = 512;

        /// <summary>Impronta da pixel RGBA 252 × 352 (riga per riga).</summary>
        public static sbyte[] FromRgba(byte[] rgba)
        {
            if (rgba.Length != InputWidth * InputHeight * 4) throw new ArgumentException("Servono pixel RGBA 252 × 352", nameof(rgba));

            // Media 2 × 2 con il bianco sotto le parti trasparenti
            var basePx = new double[BaseWidth * BaseHeight * 3];
            for (var y = 0; y < BaseHeight; y++)
            for (var x = 0; x < BaseWidth; x++)
            {
                double r = 0, g = 0, b = 0;
                for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    var i = (((y * 2 + dy) * InputWidth) + x * 2 + dx) * 4;
                    var a = rgba[i + 3] / 255.0;
                    r += rgba[i] * a + 255 * (1 - a);
                    g += rgba[i + 1] * a + 255 * (1 - a);
                    b += rgba[i + 2] * a + 255 * (1 - a);
                }
                var o = (y * BaseWidth + x) * 3;
                basePx[o] = r / 4; basePx[o + 1] = g / 4; basePx[o + 2] = b / 4;
            }

            var ix = (int)Math.Floor(BaseWidth * Inset);
            var iy = (int)Math.Floor(BaseHeight * Inset);
            var w = BaseWidth - 2 * ix;
            var h = BaseHeight - 2 * iy;

            // Medie per cella
            var cells = new double[Rows * Cols * 3];
            for (var j = 0; j < Rows; j++)
            for (var i = 0; i < Cols; i++)
            {
                int x0 = i * w / Cols, x1 = (i + 1) * w / Cols, y0 = j * h / Rows, y1 = (j + 1) * h / Rows;
                double r = 0, g = 0, b = 0;
                var n = 0;
                for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var o = ((y + iy) * BaseWidth + x + ix) * 3;
                    r += basePx[o]; g += basePx[o + 1]; b += basePx[o + 2];
                    n++;
                }
                var c = (j * Cols + i) * 3;
                cells[c] = r / n; cells[c + 1] = g / n; cells[c + 2] = b / n;
            }

            var color = new double[Rows * Cols * 3];
            for (var ch = 0; ch < 3; ch++)
            {
                double mean = 0;
                for (var k = 0; k < Rows * Cols; k++) mean += cells[k * 3 + ch];
                mean /= Rows * Cols;
                double variance = 0;
                for (var k = 0; k < Rows * Cols; k++) variance += Math.Pow(cells[k * 3 + ch] - mean, 2);
                var std = Math.Sqrt(variance / (Rows * Cols)) + 1e-3;
                for (var k = 0; k < Rows * Cols; k++) color[k * 3 + ch] = (cells[k * 3 + ch] - mean) / std;
            }
            Normalize(color);

            var lum = new double[Rows * Cols];
            for (var k = 0; k < Rows * Cols; k++) lum[k] = (cells[k * 3] + cells[k * 3 + 1] + cells[k * 3 + 2]) / 3;
            var edges = new double[Rows * (Cols - 1) + (Rows - 1) * Cols];
            var e = 0;
            for (var j = 0; j < Rows; j++)
                for (var i = 0; i < Cols - 1; i++) edges[e++] = lum[j * Cols + i + 1] - lum[j * Cols + i];
            for (var j = 0; j < Rows - 1; j++)
                for (var i = 0; i < Cols; i++) edges[e++] = lum[(j + 1) * Cols + i] - lum[j * Cols + i];
            double em = edges.Average();
            var es = Math.Sqrt(edges.Sum(v => (v - em) * (v - em)) / edges.Length) + 1e-3;
            for (var k = 0; k < edges.Length; k++) edges[k] = (edges[k] - em) / es;
            Normalize(edges);

            var result = new sbyte[Length];
            var half = Math.Sqrt(0.5);
            for (var k = 0; k < color.Length; k++) result[k] = Quantize(color[k] * half);
            for (var k = 0; k < edges.Length; k++) result[color.Length + k] = Quantize(edges[k] * half);
            return result;
        }

        private static void Normalize(double[] v)
        {
            var norm = Math.Sqrt(v.Sum(x => x * x));
            if (norm < 1e-9) return;
            for (var i = 0; i < v.Length; i++) v[i] /= norm;
        }

        private static sbyte Quantize(double v) => (sbyte)Math.Clamp(Math.Round(v * Scale), -127, 127);

        /// <summary>Somiglianza tra due impronte (coseno, da -1 a 1).</summary>
        public static double Similarity(ReadOnlySpan<sbyte> a, ReadOnlySpan<sbyte> b)
        {
            long dot = 0, na = 0, nb = 0;
            for (var i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt((double)na * nb);
        }

        public static byte[] ToBytes(sbyte[] v)
        {
            var b = new byte[v.Length];
            Buffer.BlockCopy(v, 0, b, 0, v.Length);
            return b;
        }

        public static sbyte[] FromBytes(byte[] v)
        {
            var s = new sbyte[v.Length];
            Buffer.BlockCopy(v, 0, s, 0, v.Length);
            return s;
        }
    }
}
