#!/usr/bin/env python3
"""wikiupload.py [--dry-run] [--package Largo/POGLib] [--wiki <dir>]

Uploads wiki/NN-*.md to a package's wiki on Thunderstore (wiki/README.md says how the wiki is organised).

- A page's title is its first line without "# " ("# 02 Your first item" -> "02 Your first item"); the rest is its
  Markdown. A page whose title already exists on the site is updated in place (same id, same address).
- Links between pages are written as relative file names in the repository ("(02-your-first-item.md)"); the site
  needs full addresses with the page's id, which exists only once the page does. So pass 1 creates or updates every
  page, pass 2 rewrites the links to the ids the site returned and updates the pages whose text changed.
- Pages on the site that have no file here are listed, never deleted.
- Run tools/wikicheck first: it compiles every C# block (this script does not run it).

Auth: a browser session, $THUNDERSTORE_SESSIONID and $THUNDERSTORE_CSRFTOKEN (the cookies of a logged-in
thunderstore.io, as the publish script's --deprecate takes them): the service account's token publishes packages but
may not edit a wiki ("You do not have permission to perform this action", 2026-11-23). Without the session the
token is tried. Neither is ever printed.
"""
import argparse, json, os, pathlib, re, sys, urllib.request

API = "https://thunderstore.io/api/experimental"
SITE = "https://thunderstore.io/c/pit-of-goblin/p"


def token():
    t = os.environ.get("THUNDERSTORE_TOKEN")
    if t: return t.strip()
    p = pathlib.Path.home() / ".config" / "thunderstore" / "token"
    return p.read_text().strip()


def call(method, url, body=None, auth=True):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={"User-Agent": "pog-wiki-upload", "Content-Type": "application/json"})
    sid, csrf = os.environ.get("THUNDERSTORE_SESSIONID"), os.environ.get("THUNDERSTORE_CSRFTOKEN")
    if auth and sid and csrf:
        req.add_header("Cookie", f"sessionid={sid}; csrftoken={csrf}")
        req.add_header("X-CSRFToken", csrf)
        req.add_header("Referer", "https://thunderstore.io/")
    elif auth:
        req.add_header("Authorization", f"Bearer {token()}")
    with urllib.request.urlopen(req, timeout=60) as r:
        raw = r.read()
        return json.loads(raw) if raw else None


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
    ap.add_argument("--package", default="Largo/POGLib")
    ap.add_argument("--wiki", default=str(pathlib.Path(__file__).resolve().parent.parent / "wiki"))
    a = ap.parse_args()
    ns, name = a.package.split("/")
    pages = load_pages(a.wiki)
    try:
        site = call("GET", f"{API}/package/{ns}/{name}/wiki/", auth=False)
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
        r = call("POST", f"{API}/package/{ns}/{name}/wiki/", body)
        ids[p["file"]] = (r["id"], r["slug"])
        print(f"pass 1: {p['title']} -> {r['id']}-{r['slug']}")
    # Pass 2: links to the real addresses.
    addresses = {f: f"{SITE}/{ns}/{name}/wiki/{i}-{s}/" for f, (i, s) in ids.items()}
    for p in pages:
        linked = link(p["markdown"], addresses)
        if linked != p["markdown"]:
            call("POST", f"{API}/package/{ns}/{name}/wiki/", {"id": ids[p["file"]][0], "title": p["title"], "markdown_content": linked})
            print(f"pass 2: links in {p['title']}")
    print(f"{len(pages)} page(s): {SITE}/{ns}/{name}/wiki/")


if __name__ == "__main__":
    main()
