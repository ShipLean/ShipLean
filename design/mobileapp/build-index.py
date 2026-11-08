#!/usr/bin/env python3
"""Builds index.html, the static review page: every state of every area as light/dark screenshots, plus each area's
decisions and open questions from its .md file and the tenet and shared decisions from README.md.

Usage: ./build-index.py                       writes index.html (images linked from images/)
       ./build-index.py --inline inbox --out x.html   one self-contained page for chosen areas (images inlined), for attaching to a work item
Run ./screenshots.sh first. CHROME=/path/to/chrome overrides the browser; LD_LIBRARY_PATH is passed through.
"""
import argparse, base64, html, json, os, re, shutil, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
AREAS = [  # slug, name, one-line purpose
    ('inbox', 'Inbox', 'The first tab: everything waiting on you, longest waiting first, answered from the row by a tap, typing or voice.'),
    ('projects', 'Projects', 'Projects is the root of the app: pick a project, and its key tile at the top left of every tab takes you back.'),
    ('work-item', 'Work item', 'A Feature or Story on the phone: status first, then what waits on you, then notes. Most commands leave a note.'),
    ('backlog', 'Backlog', 'Priority: rank order of Epics, Features and Stories, changed by voice ("move SHIP-906 above SHIP-907") or a Move sheet.'),
    ('feature-board', 'Feature board', 'Keep an eye on progress across Features: health, the status line and how fresh it is, progress, and what needs you.'),
    ('retro', 'Retro', 'The current sprint\'s retro: dictate points, +1, turn a point into an action item, and settle what agents left to decide.'),
    ('setup', 'Install and setup', 'First run: sign in, choose the voice models, download them while the app stays usable, and change them later in Settings.'),
]
HEAD = '<!doctype html>\n<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">\n<title>Phone app mock-ups (SHIP-905)</title><link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap"><link rel="stylesheet" href="../tokens.css">\n<style>\n*,*::before,*::after{box-sizing:border-box} body{margin:0;background:var(--color-bg);color:var(--color-text);font:14px/1.5 var(--font-sans)}\nmain{max-width:1320px;margin:0 auto;padding:28px 20px 72px} h1{font-size:26px;margin:0 0 6px} h2{font-size:20px;margin:44px 0 4px} h2 small{font-size:13px;font-weight:400;color:var(--color-muted)}\nh3{font-size:12px;text-transform:uppercase;letter-spacing:.05em;color:var(--color-muted);margin:0 0 8px} .lede{color:var(--color-muted);max-width:860px;margin:0}\n.bar{position:sticky;top:0;z-index:2;background:var(--color-bg);border-bottom:1px solid var(--color-border)} .bar div{max-width:1320px;margin:0 auto;padding:10px 20px;display:flex;gap:16px;flex-wrap:wrap;align-items:center}\n.bar a{color:var(--color-text);text-decoration:none;font-weight:500;font-size:13px} .bar .grow{flex:1} .bar button{font:inherit;font-size:12.5px;border:1px solid var(--color-border);background:var(--color-surface);color:var(--color-text);border-radius:6px;padding:4px 12px;cursor:pointer}\n.phones{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:22px 16px;margin-top:14px} .ph{margin:0;min-width:0}\n.ph img{width:240px;max-width:100%;border-radius:22px;border:5px solid #10151f;display:block;box-shadow:var(--shadow-raised)} .ph .dk{display:none} body.dark .ph .lt{display:none} body.dark .ph .dk{display:block}\n.ph figcaption{margin-top:8px;max-width:240px;font-size:12.5px;color:var(--color-muted)} .ph figcaption b{display:block;color:var(--color-text);font-size:13px} .ph figcaption span{display:block} .ph a{color:var(--color-accent)}\n.cols{display:grid;grid-template-columns:minmax(0,1.4fr) minmax(0,1fr);gap:16px;margin-top:18px} @media(max-width:860px){.cols{grid-template-columns:minmax(0,1fr)}}\n.panel{background:var(--color-surface);border:1px solid var(--color-border);border-radius:12px;padding:16px 18px;min-width:0} .panel ul,.panel ol{margin:0;padding-left:18px} .panel li{margin:6px 0;overflow-wrap:anywhere}\nol.dec{list-style:none;padding:0} .ans{display:block;color:var(--color-ok)} .open{font-size:11px;font-weight:600;color:var(--color-warn)} em{font-style:normal;font-weight:600;color:var(--color-muted)}\ncode{font-family:var(--font-mono);font-size:12px;background:var(--color-surface-2);padding:1px 5px;border-radius:4px}\n.intro{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:16px;margin-top:18px} @media(max-width:860px){.intro{grid-template-columns:minmax(0,1fr)}}\n</style></head>\n'

def chrome():
    c = os.environ.get('CHROME')
    if c: return c
    for n in ('google-chrome', 'chromium', 'chromium-browser', 'chrome-headless-shell'):
        if shutil.which(n): return n
    raise SystemExit('No Chrome found; set CHROME=/path/to/chrome')

def meta(slug):
    out = subprocess.run([chrome(), '--headless', '--no-sandbox', '--virtual-time-budget=3000', '--dump-dom', f'file://{HERE}/{slug}.html?meta'], capture_output=True, text=True).stdout
    return json.loads(re.search(r'APPMETA(.*?)ENDMETA', out, re.S).group(1))

def inline(s):  # the bit of Markdown the notes use: **bold**, *em*, `code`
    s = html.escape(s, quote=False)
    s = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', s)
    s = re.sub(r'(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', r'<em>\1</em>', s)
    return re.sub(r'`(.+?)`', r'<code>\1</code>', s)

def sections(md):
    parts = re.split(r'^## (.+)$', md, flags=re.M)
    return [(parts[i].strip(), parts[i + 1]) for i in range(1, len(parts), 2)]

def bullets(body):
    return [m for m in re.findall(r'^- (.+)$', body, re.M)]

def block(body):  # paragraphs and one level of bullets
    out, items = [], []
    for para in re.split(r'\n\s*\n', body.strip()):
        lines = [l for l in para.splitlines() if l.strip()]
        if lines and all(l.startswith(('- ', '  ')) for l in lines):
            merged = []
            for l in lines:
                if l.startswith('- '): merged.append(l[2:])
                elif merged: merged[-1] += ' ' + l.strip().lstrip('- ')
            out.append('<ul>' + ''.join(f'<li>{inline(x)}</li>' for x in merged) + '</ul>')
        elif lines:
            out.append(f'<p>{inline(" ".join(lines))}</p>')
    return ''.join(out)

def img(path, embed):
    if not embed: return path
    return 'data:image/png;base64,' + base64.b64encode(open(os.path.join(HERE, path), 'rb').read()).decode()

def area_html(slug, name, lede, embed):
    m = meta(slug)
    md = open(os.path.join(HERE, f'{slug}.md')).read()
    secs = sections(md)
    dec = [b for t, body in secs if t.startswith('Decisions') for b in bullets(body) if re.match(r'\*\*D\d', b)]
    decided = [(t, bullets(body)) for t, body in secs if t.startswith('Decided')]
    oq = [b for t, body in secs if t.startswith('Open questions') for b in bullets(body)]
    figs = []
    for s in m['states']:
        sid = s['id']
        lt, dk = img(f'images/{slug}-{sid}.light.png', embed), img(f'images/{slug}-{sid}.dark.png', embed)
        live = '' if embed else f'<a href="{slug}.html?state={sid}">live page</a> · '
        figs.append(f'<figure class="ph"><img class="lt" loading="lazy" alt="{html.escape(name)}: {html.escape(s["title"])}, light" src="{lt}">'
                    f'<img class="dk" loading="lazy" alt="{html.escape(name)}: {html.escape(s["title"])}, dark" src="{dk}">'
                    f'\n      <figcaption><b>{html.escape(s["title"])}</b><span>{html.escape(s.get("note") or "")}</span>{live}<code>?state={sid}</code></figcaption></figure>')
    dpanel = f'<div class="panel"><h3>Decisions</h3><ol class="dec">' + ''.join(f'<li>{inline(d)}</li>' for d in dec) + '</ol>' + \
        ''.join(f'<h3 style="margin-top:16px">{inline(t)}</h3><ul>' + ''.join(f'<li>{inline(b)}</li>' for b in bs) + '</ul>' for t, bs in decided) + '</div>'
    qpanel = '<div class="panel"><h3>Open questions</h3><ul>' + ''.join(f'<li>{inline(q)}</li>' for q in oq) + '</ul></div>'
    return (f'<section id="{slug}"><h2>{html.escape(name)} <small>{len(m["states"])} states · <code>{slug}.html</code> · notes in <code>{slug}.md</code></small></h2>\n'
            f'    <p class="lede">{inline(lede)}</p><div class="phones">' + ''.join(figs) + f'</div>\n    <div class="cols">{dpanel}\n    {qpanel}</div></section>'), len(m['states'])

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--inline', help='comma-separated area slugs; inlines their images into one self-contained page')
    ap.add_argument('--out', default=os.path.join(HERE, 'index.html'))
    a = ap.parse_args()
    pick = a.inline.split(',') if a.inline else [s for s, _, _ in AREAS]
    areas = [x for x in AREAS if x[0] in pick]
    built = [area_html(*x, bool(a.inline)) for x in areas]
    readme = open(os.path.join(HERE, 'README.md')).read()
    shared = ''.join(f'<div class="panel"><h3>{inline(t)}</h3>{block(body)}</div>' for t, body in sections(readme) if not t.startswith('Shared voice'))
    nav = '<a href="#shared">Tenet and shared</a>' + ''.join(f'<a href="#{s}">{html.escape(n)}</a>' for s, n, _ in areas)
    total = sum(n for _, n in built)
    title = 'Phone app mock-ups' + (f': {", ".join(n for _, n, _ in areas)}' if a.inline else '')
    head = HEAD
    if a.inline:  # an attachment has no ../tokens.css next to it
        head = head.replace('<link rel="stylesheet" href="../tokens.css">', '<style>' + open(os.path.join(HERE, '..', 'tokens.css')).read() + '</style>')
    page = (head.replace('<title>Phone app mock-ups (SHIP-905)</title>', f'<title>{html.escape(title)} (SHIP-905)</title>') +
        f'<body><div class="bar"><div><b>Phone app · SHIP-905</b>{nav}<span class="grow"></span><button type="button" onclick="document.body.classList.toggle(\'dark\');this.textContent=document.body.classList.contains(\'dark\')?\'Show light\':\'Show dark\'">Show dark</button></div></div>\n'
        f'<main><h1>{html.escape(title)}</h1>\n<p class="lede">The native app (epic SHIP-905) does less than the web app. Voice goes in and text comes out: notes, keeping an eye on progress, answering what people and agents wait on you for, and changing priority. {len(areas)} area{"s" if len(areas) != 1 else ""}, {total} states, light and dark, at 390 × 844. Throwaway HTML on a small kit (<code>app.css</code>, <code>app.js</code>) over <code>../tokens.css</code>; not the Expo app.</p>\n'
        f'<div class="intro" id="shared">{shared}</div>\n' + '\n'.join(h for h, _ in built) + '\n</main></body></html>\n')
    open(a.out, 'w').write(page)
    print(a.out, f'{len(areas)} areas, {total} states, {len(page) // 1024} KB')

if __name__ == '__main__':
    main()
