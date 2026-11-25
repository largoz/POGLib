# The wiki: how it is organised

The modder's documentation lives here as Markdown files and is uploaded to the **POGLib package's wiki on
Thunderstore** by `tools/wikiupload.py` (Thunderstore's API: `POST /api/experimental/package/Largo/POGLib/wiki/`).
Only a member of the team may write a wiki, the publish token may not, so the script sends from a logged-in browser
tab. One copy of the text, in git; nothing is edited by hand on the site. POGShops' wiki holds one page that points
here: `shops/00-start-here.md` (`--package Largo/POGShops --wiki wiki/shops`). Agreed with the user, 2026-11-23.

## What Thunderstore's wiki is (checked 2026-11-23 through its API and other packages' wikis)

- One wiki per package; pages are flat: a title and Markdown, no sub-pages.
- Pages are listed **alphabetically by title**, so the order comes from a number at the start of each title.
- Links between pages are ordinary full URLs (`…/p/Largo/POGLib/wiki/<slug>/`; the slug the API returns already
  starts with the id, `6976-00-start-here`); the id is known only after a page is created, so the upload script creates pages first and rewrites links second.
- Links to a place inside a page do not work: headings carry no anchors, on package pages and on wiki pages
  (checked on the live wiki 2026-11-25).
- The site's CDN caches answers for about five minutes, a 404 too: a new page may show "not found" for that long.

## The map

Tens are sections, so a page can be added later without renumbering the rest. A file here is named like its title:
`00-start-here.md` is the page "00 Start here".

| No. | Page | What it holds | For 0.1 |
|---|---|---|---|
| 00 | Start here | The map: what a mod can add, what is shown in the game, links to everything | yes |
| 01 | Set up a mod project | The project, references to the DLLs, dependencies | yes |
| 02 | Your first item | An item in about 15 lines, and how to see it in the game | yes |
| 03 | Publish your mod | Manifest, dependencies, package layout | yes |
| 10 | Items: all fields | Reference: every field of an item, as a table | yes |
| 11 | Items: models and icons | OBJ, materials, icon, how an item stands | yes |
| 12 | Items: capabilities | Food, glow, weapons…: each with its table of fields and an example | yes |
| 13 | Items: use handlers | A use of your own | later |
| 14 | Items: your own capability | Writing an open effect class others can use | later |
| 20 | Shops: your first slot | A slot in five lines | yes |
| 21 | Shops: the shop file | Reference for `pogshop.json` | yes |
| 22 | Shops: sellers and scenes | The game's sellers per scene, with their ids | yes |
| 23 | Shops: dungeon merchants | Merchants at the dungeon's entrance: classes, chances, the host's settings | yes |
| 30 | Mobs | Reserved | later |
| 40 | Levels | Reserved | later |
| 50 | Cosmetics: your first part | Clothes, hair, horns, accessories; locked parts; NPCs; first person | yes |
| 51 | Cosmetics: the model | GLB, the goblin's measures and bones, materials | yes |
| 52 | Cosmetics: the game's parts and colours | The game's own parts and palettes by name and index | yes |
| 80 | Multiplayer and saves | What every player needs, saves, POG Mod Check | yes |
| 90 | Troubleshooting | Log lines and what they mean | yes |
| 91 | What will not change | What the library promises not to break | later |
| 95 | Community capabilities | Open classes by other authors | later |

## A section page, the same shape every time (items, shops, mobs, levels)

1. What it is, in two or three sentences, and its state: shown in the game or experimental.
2. The smallest example that works.
3. A table of every field.
4. Step by step, with screenshots where words are not enough.
5. Common mistakes (with the log line each one gives).
6. Full examples at the end.
7. Links to the neighbouring pages.

## Rules

- A page says what is **shown in the game** and what is **experimental**, the same split as the store page.
- Every code example compiles: the upload script refuses a page whose ```csharp block does not build against the
  current `POGLib.dll` and `POGShops.dll` (as the store README's sample was checked on 2026-11-22).
- Game art (screenshots are fine; extracted sprites and models are not) stays out of the repository.
