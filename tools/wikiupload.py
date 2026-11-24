#!/usr/bin/env python3
"""wikiupload.py [--dry-run] [--browser PORT] [--package Largo/POGLib] [--wiki <dir>]

Uploads wiki/NN-*.md to a package's wiki on Thunderstore (wiki/README.md says how the wiki is organised).

- A page's title is its first line without "# " ("# 02 Your first item" -> "02 Your first item"); the rest is its
  Markdown. A page whose title already exists on the site is updated in place (same id, same address).
- Links between pages are written as relative file names in the repository ("(02-your-first-item.md)"); the site
  needs full addresses with the page's id, which exists only once the page does. So pass 1 creates or updates every
  page, pass 2 rewrites the links to the ids the site returned and updates the pages whose text changed.
- Pages on the site that have no file here are listed, never deleted.
- Run tools/wikicheck first: it compiles every C# block (this script does not run it).

Auth: the wiki can be written only by a member of the team, not by a service account (the site's own rule: the
publish token gets 403). So the requests are sent from inside a logged-in thunderstore.io tab of a Chromium started
with --remote-debugging-port, over its DevTools port: --browser PORT (default 9224). The page reads its own session
and sends it; the session never leaves the browser, is not printed and is not stored. Reading the wiki (and --dry-run)
needs no login. Needs Python's websockets.
"""
import argparse, asyncio, json, pathlib, re, sys, time, urllib.error, urllib.request

API = "https://thunderstore.io/api/experimental"
SITE = "https://thunderstore.io/c/pit-of-goblin/p"


def get(url):
    # The site's CDN caches answers, a 404 too: a cached one would make every page "new" and upload it twice.
    req = urllib.request.Request(f"{url}?nocache={time.time_ns()}", headers={"User-Agent": "pog-wiki-upload"})
    with urllib.request.urlopen(req, timeout=60) as r: return json.loads(r.read())


# Runs in the page: its session cookie is readable to the site's own scripts (the site sends it the same way).
IN_PAGE = """(async (url, body) => {
  if (location.host !== "thunderstore.io") return JSON.stringify({error: "not on thunderstore.io"});
  const m = document.cookie.match(/(?:^|;\\s*)sessionid=([^;]+)/);
  if (!m) return JSON.stringify({error: "not logged in"});
  const r = await fetch(url, {method: "POST", credentials: "omit", body: JSON.stringify(body),
    headers: {"Content-Type": "application/json", "Authorization": "Session " + m[1]}});
  return JSON.stringify({status: r.status, text: await r.text()});
})(%s, %s)"""


async def evaluate(port, expression):
    import websockets
    tabs = json.load(urllib.request.urlopen(f"http://127.0.0.1:{port}/json", timeout=10))
    tab = next((t for t in tabs if t["type"] == "page" and t["url"].startswith("https://thunderstore.io/")), None)
    if tab is None: sys.exit("no thunderstore.io tab in that browser")
    async with websockets.connect(tab["webSocketDebuggerUrl"], max_size=None) as ws:
        await ws.send(json.dumps({"id": 1, "method": "Runtime.evaluate", "params": {
            "expression": expression, "awaitPromise": True, "returnByValue": True}}))
        while True:
            r = json.loads(await ws.recv())
            if r.get("id") == 1: return r["result"]["result"].get("value")


def post(port, url, body):
    raw = asyncio.run(evaluate(port, IN_PAGE % (json.dumps(url), json.dumps(body))))
    r = json.loads(raw or '{"error": "the page gave no answer"}')
    if "error" in r: sys.exit(r["error"])
    if r["status"] not in (200, 201): sys.exit(f"HTTP {r['status']}: {r['text'][:300]}")
    return json.loads(r["text"])


def load_pages(wiki_dir):
    pages = []
    for f in sorted(pathlib.Path(wiki_dir).glob("[0-9][0-9]-*.md")):
        text = f.read_text(encoding="utf-8")
        first, _, rest = text.partition("\n")
        if not first.startswith("# "): sys.exit(f"{f.name}: the first line must be '# NN Title'")
        pages.append({"file": f.name, "title": first[2:].strip(), "markdown": rest.lstrip("\n")})
    return pages


def link(markdown, addresses):
    # (02-your-first-item.md) and (02-your-first-item.md#part) -> the page's address on the site
    def sub(m):
        f, anchor = m.group(1), m.group(2) or ""
        return f"({addresses[f]}{anchor})" if f in addresses else m.group(0)
    return re.sub(r"\((\d\d-[a-z0-9-]+\.md)(#[^)]*)?\)", sub, markdown)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--browser", type=int, default=9224, metavar="PORT")
    ap.add_argument("--package", default="Largo/POGLib")
    ap.add_argument("--wiki", default=str(pathlib.Path(__file__).resolve().parent.parent / "wiki"))
    a = ap.parse_args()
    ns, name = a.package.split("/")
    pages = load_pages(a.wiki)
    try:
        site = get(f"{API}/package/{ns}/{name}/wiki/")
        existing = {p["title"]: p for p in site.get("pages", [])}
    except urllib.error.HTTPError as e:
        if e.code != 404: raise
        existing = {}
    mine = {p["title"] for p in pages}
    for t in existing:
        if t not in mine: print(f"on the site, no file here (left alone): {t}")
    unresolved = set()
    for p in pages:
        for f in re.findall(r"\((\d\d-[a-z0-9-]+\.md)", p["markdown"]):
            if f not in {q["file"] for q in pages}: unresolved.add((p["file"], f))
    for page, f in sorted(unresolved): print(f"{page}: links to {f}, which has no file (the link stays as written)")
    if a.dry_run:
        for p in pages: print(("update " if p["title"] in existing else "create ") + p["title"])
        return
    # Pass 1: every page exists, with its links still relative.
    ids = {}
    for p in pages:
        body = {"title": p["title"], "markdown_content": p["markdown"]}
        if p["title"] in existing: body["id"] = existing[p["title"]]["id"]
        r = post(a.browser, f"{API}/package/{ns}/{name}/wiki/", body)
        ids[p["file"]] = (r["id"], r["slug"])
        print(f"pass 1: {p['title']} -> {r['slug']}")
    # Pass 2: links to the real addresses.
    # The slug already starts with the id ("6976-00-start-here").
    addresses = {f: f"{SITE}/{ns}/{name}/wiki/{slug}/" for f, (_, slug) in ids.items()}
    for p in pages:
        linked = link(p["markdown"], addresses)
        if linked != p["markdown"]:
            post(a.browser, f"{API}/package/{ns}/{name}/wiki/", {"id": ids[p["file"]][0], "title": p["title"], "markdown_content": linked})
            print(f"pass 2: links in {p['title']}")
    print(f"{len(pages)} page(s): {SITE}/{ns}/{name}/wiki/")


if __name__ == "__main__":
    main()
