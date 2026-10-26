#!/usr/bin/env python3
"""A5: hybrid = RRF(k=60) of a lexical rankings file and a semantic scored file whose hits below the cut-off are dropped first,
then the production exact-key rule (keyfirst) is NOT applied here (it does not change non-key rows). Prints top-5 and mean hits per
group per cut-off. Usage: hybrid_cutoff.py lexical.json semantic.json from to step"""
import json, glob, sys
sys.path.insert(0, 'scripts/search-eval')
import metrics

qs = []
for f in sorted(glob.glob('scripts/search-eval/queries/*.json')):
    qs += metrics.load(f)
lex, sem = metrics.load(sys.argv[1]), metrics.load(sys.argv[2])
lo, hi, step = (float(x) for x in sys.argv[3:6])
out = sys.argv[6] if len(sys.argv) > 6 else None  # with lo == hi: also write the fused rankings file (scores = null)
groups = {'all': lambda q: True, 'para+body': lambda q: q['kind'] in ('paraphrase', 'bodyOnly'),
          'exactKey+titleWord': lambda q: q['kind'] in ('exactKey', 'titleWord'), 'commentOnly': lambda q: q['kind'] == 'commentOnly'}

def fuse(lists, k=60):
    sc, order = {}, []
    for l in lists:
        for r, ref in enumerate(l):
            if ref not in sc:
                order.append(ref)
            sc[ref] = sc.get(ref, 0) + 1 / (k + r + 1)
    return sorted(order, key=lambda x: (-sc[x], order.index(x)))

print('| cut-off | ' + ' | '.join(f'{g} top-5 / hits' for g in groups) + ' |')
print('|---|' + '---|' * len(groups))
c = lo
while c <= hi + 1e-9:
    res = {}
    fused = {}
    for q in qs:
        s = [ref for ref, sc in sem['queries'][q['id']] if sc >= c - 1e-9]
        out_ = fuse([[r for r, _ in lex['queries'][q['id']]], s])
        fused[q['id']] = [[r, None] for r in out_]
        t5 = 1.0 if set(q['expected']) & set(out_[:5]) else 0.0
        for g, m in groups.items():
            if m(q):
                res.setdefault(g, []).append((t5, len(out_)))
    print(f'| {c:.2f} | ' + ' | '.join(f'{sum(a for a,_ in res[g])/len(res[g]):.2f} / {sum(b for _,b in res[g])/len(res[g]):.1f}' for g in groups) + ' |')
    if out and abs(lo - hi) < 1e-9:
        json.dump({'label': f'hybrid({lex["label"]}+{sem["label"]}@{c:.2f})', 'queries': fused, 'latencyMs': {k: lex['latencyMs'].get(k, 0) + sem['latencyMs'].get(k, 0) for k in fused}}, open(out, 'w'))
    c += step
