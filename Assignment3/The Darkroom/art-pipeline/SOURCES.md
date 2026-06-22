# Backdrop source images — provenance

Public-domain darkroom photographs / engravings used as raw input for
`gen_backdrops.py`. Download into `_src/` (gitignored), then run the script to
produce the palette-matched `bd_*.png` in `../Assets/StreamingAssets/art/`.

To re-fetch (Wikimedia/LoC require a descriptive User-Agent):

```bash
mkdir -p _src && cd _src
UA="DarkroomGame-StudentProject/1.0 (educational)"
curl -sL -A "$UA" -o gartenlaube_1894.jpg "https://upload.wikimedia.org/wikipedia/commons/f/f1/Die_Gartenlaube_%281894%29_b_605_2.jpg"
curl -sL -A "$UA" -o cornell_1917.jpg      "https://upload.wikimedia.org/wikipedia/commons/3/38/Colleges_and_Universities_-_Cornell_University_-_one_of_the_dark_rooms_-_NARA_-_26425820.jpg"
curl -sL -A "$UA" -o sellwood_1956.jpg     "https://upload.wikimedia.org/wikipedia/commons/c/cc/1956._Print_processing_darkroom_at_Sellwood_Lab._Portland%2C_Oregon._%2828338744449%29.jpg"
curl -sL -A "$UA" -o jeffcoat_loc.jpg      "https://tile.loc.gov/storage-services/service/pnp/highsm/67100/67105v.jpg"
```

| Output | Source image | Repository | License |
|---|---|---|---|
| `bd_cornell.png` | Cornell University — one of the dark rooms (NARA 26425820, 1917) | Wikimedia Commons / US National Archives | Public Domain (PD-USGov) |
| `bd_sellwood.png` | Print processing darkroom, Sellwood Lab, Portland OR (1956) | Wikimedia Commons / USDA Forest Service | Public Domain (PD-USGov-USDA) |
| `bd_engraving.png` | "In der Dunkelkammer", *Die Gartenlaube* p.605 (1894) | Wikimedia Commons | Public Domain (PD-old, pub. 1894) |
| `bd_jeffcoat.png` | Jeffcoat Photography Studio darkroom, Abilene KS — Carol M. Highsmith | Library of Congress (Highsmith Archive) | Public Domain (no known restrictions) |

The CC0 wall/material candidates the research surfaced (PolyHaven `plastered_wall_05`,
ambientCG `Concrete033/015/017`, `dark_wooden_planks`, …) were **not** used: photoreal
textures clash with the stylised silhouette look, so the walls are authored procedurally
(`gen_walls.py`) instead. Kept here only as a fallback option if a photoreal surface is ever wanted.
