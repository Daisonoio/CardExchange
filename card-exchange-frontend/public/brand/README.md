# Logo CardExchange

File vettoriali (SVG): restano nitidi a qualsiasi dimensione. Palette del sito, senza oro.

| File | Uso |
|---|---|
| `logo-horizontal.svg` | simbolo + scritta affiancati, su sfondo chiaro (intestazioni, email, documenti) |
| `logo-horizontal-inverse.svg` | come sopra, su sfondo blu |
| `logo-stacked.svg` | simbolo sopra la scritta, su sfondo chiaro (copertine, social, locandine) |
| `logo-stacked-inverse.svg` | come sopra, su sfondo blu |
| `logo-mark.svg` / `logo-mark-inverse.svg` | solo simbolo (avatar, spazi piccoli) |
| `icon-maskable.svg` e `../icon.svg` | icona dell'app (favicon, schermata Home del telefono) |

Colori: blu `#1a3461` (primario), `#122548`, `#2a4d8f`, azzurro `#3d7be0`, azzurro chiaro `#8db8ff`, bianco.
Scritta: Inter Display Black Italic, convertita in tracciati (non serve il font).

Per modificarli: `python3 tools/brand/build_logo.py` (richiede `fonttools` e `uharfbuzz`),
poi `node tools/brand/render_icons.cjs` per le icone PNG. Nel codice si usa il componente `BrandLogo`.
