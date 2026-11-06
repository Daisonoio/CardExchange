/**
 * Impronta visiva di una carta, calcolata sul telefono: al server arrivano solo questi numeri, mai la foto.
 * Deve restare identica a CardExchange.Core/Rules/CardFingerprint.cs (stessi passi, stesse costanti).
 */
export const FP_INPUT_W = 252;
export const FP_INPUT_H = 352;
const BASE_W = 126;
const BASE_H = 176;
const COLS = 12;
const ROWS = 16;
const INSET = 0.05;
const SCALE = 512;
export const FP_LENGTH = COLS * ROWS * 3 + ROWS * (COLS - 1) + (ROWS - 1) * COLS; // 932

export type Rect = { x: number; y: number; w: number; h: number };

function normalize(v: Float64Array) {
  let n = 0;
  for (let i = 0; i < v.length; i++) n += v[i] * v[i];
  n = Math.sqrt(n);
  if (n < 1e-9) return;
  for (let i = 0; i < v.length; i++) v[i] /= n;
}

const quantize = (v: number) => Math.max(-127, Math.min(127, Math.round(v * SCALE)));

/** Impronta da pixel RGBA 252 × 352. */
export function fingerprintFromRgba(rgba: Uint8ClampedArray): Int8Array {
  const base = new Float64Array(BASE_W * BASE_H * 3);
  for (let y = 0; y < BASE_H; y++) {
    for (let x = 0; x < BASE_W; x++) {
      let r = 0, g = 0, b = 0;
      for (let dy = 0; dy < 2; dy++) {
        for (let dx = 0; dx < 2; dx++) {
          const i = (((y * 2 + dy) * FP_INPUT_W) + x * 2 + dx) * 4;
          const a = rgba[i + 3] / 255;
          r += rgba[i] * a + 255 * (1 - a);
          g += rgba[i + 1] * a + 255 * (1 - a);
          b += rgba[i + 2] * a + 255 * (1 - a);
        }
      }
      const o = (y * BASE_W + x) * 3;
      base[o] = r / 4; base[o + 1] = g / 4; base[o + 2] = b / 4;
    }
  }

  const ix = Math.floor(BASE_W * INSET);
  const iy = Math.floor(BASE_H * INSET);
  const w = BASE_W - 2 * ix;
  const h = BASE_H - 2 * iy;
  const cells = new Float64Array(ROWS * COLS * 3);
  for (let j = 0; j < ROWS; j++) {
    for (let i = 0; i < COLS; i++) {
      const x0 = Math.floor((i * w) / COLS), x1 = Math.floor(((i + 1) * w) / COLS);
      const y0 = Math.floor((j * h) / ROWS), y1 = Math.floor(((j + 1) * h) / ROWS);
      let r = 0, g = 0, b = 0, n = 0;
      for (let y = y0; y < y1; y++) {
        for (let x = x0; x < x1; x++) {
          const o = ((y + iy) * BASE_W + x + ix) * 3;
          r += base[o]; g += base[o + 1]; b += base[o + 2];
          n++;
        }
      }
      const c = (j * COLS + i) * 3;
      cells[c] = r / n; cells[c + 1] = g / n; cells[c + 2] = b / n;
    }
  }

  const k = ROWS * COLS;
  const color = new Float64Array(k * 3);
  for (let ch = 0; ch < 3; ch++) {
    let mean = 0;
    for (let i = 0; i < k; i++) mean += cells[i * 3 + ch];
    mean /= k;
    let variance = 0;
    for (let i = 0; i < k; i++) variance += (cells[i * 3 + ch] - mean) ** 2;
    const std = Math.sqrt(variance / k) + 1e-3;
    for (let i = 0; i < k; i++) color[i * 3 + ch] = (cells[i * 3 + ch] - mean) / std;
  }
  normalize(color);

  const lum = new Float64Array(k);
  for (let i = 0; i < k; i++) lum[i] = (cells[i * 3] + cells[i * 3 + 1] + cells[i * 3 + 2]) / 3;
  const edges = new Float64Array(ROWS * (COLS - 1) + (ROWS - 1) * COLS);
  let e = 0;
  for (let j = 0; j < ROWS; j++) for (let i = 0; i < COLS - 1; i++) edges[e++] = lum[j * COLS + i + 1] - lum[j * COLS + i];
  for (let j = 0; j < ROWS - 1; j++) for (let i = 0; i < COLS; i++) edges[e++] = lum[(j + 1) * COLS + i] - lum[j * COLS + i];
  let em = 0;
  for (let i = 0; i < edges.length; i++) em += edges[i];
  em /= edges.length;
  let ev = 0;
  for (let i = 0; i < edges.length; i++) ev += (edges[i] - em) ** 2;
  const es = Math.sqrt(ev / edges.length) + 1e-3;
  for (let i = 0; i < edges.length; i++) edges[i] = (edges[i] - em) / es;
  normalize(edges);

  const out = new Int8Array(FP_LENGTH);
  const half = Math.sqrt(0.5);
  for (let i = 0; i < color.length; i++) out[i] = quantize(color[i] * half);
  for (let i = 0; i < edges.length; i++) out[color.length + i] = quantize(edges[i] * half);
  return out;
}

/** Impronta di una zona di un'immagine (fotogramma della fotocamera o foto caricata). */
export function fingerprintOf(source: CanvasImageSource, r: Rect): Int8Array {
  const canvas = document.createElement('canvas');
  canvas.width = FP_INPUT_W;
  canvas.height = FP_INPUT_H;
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(source, r.x, r.y, r.w, r.h, 0, 0, FP_INPUT_W, FP_INPUT_H);
  return fingerprintFromRgba(ctx.getImageData(0, 0, FP_INPUT_W, FP_INPUT_H).data);
}

/**
 * Più impronte della stessa zona, leggermente spostate e ingrandite: la carta raramente è
 * allineata alla guida al millimetro, e il server tiene la più somigliante.
 */
export function fingerprintVariants(source: CanvasImageSource, r: Rect): Int8Array[] {
  const out: Int8Array[] = [fingerprintOf(source, r)];
  for (const sc of [0.94, 1]) {
    for (const dx of [-0.03, 0, 0.03]) {
      for (const dy of [-0.03, 0, 0.03]) {
        if (sc === 1 && dx === 0 && dy === 0) continue;
        if (out.length >= 9) return out;
        const w = r.w * sc, h = r.h * sc;
        out.push(fingerprintOf(source, { x: r.x + (r.w - w) / 2 + dx * r.w, y: r.y + (r.h - h) / 2 + dy * r.h, w, h }));
      }
    }
  }
  return out;
}

export function toBase64(v: Int8Array): string {
  const bytes = new Uint8Array(v.buffer, v.byteOffset, v.byteLength);
  let s = '';
  for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
  return btoa(s);
}
