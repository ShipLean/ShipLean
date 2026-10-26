#!/usr/bin/env python3
"""Writes the tokenizer parity fixture: ids from Python transformers at the pinned revision.

Usage: python scripts/generate-e5-token-ids.py <out.json>   (needs: transformers sentencepiece protobuf; fetches the tokenizer)
Ids come from the fast tokenizer (tokenizer.json), which AutoTokenizer loads by default.
The .NET tokenizer must produce the same ids, special tokens included, for every string.
"""
import json
import sys

from transformers import AutoTokenizer

REPO = "intfloat/multilingual-e5-small"
REVISION = "614241f622f53c4eeff9890bdc4f31cfecc418b3"

TEXTS = [
    "query: login broken",
    "passage: Users cannot sign in after the password reset email arrives.",
    "query: Anmeldung schlägt fehl",
    "passage: Die Sprint-Planung wurde auf Donnerstag verschoben.",
    "query: ไม่สามารถเข้าสู่ระบบได้",
    "passage: ระบบแจ้งเตือนส่งอีเมลซ้ำสองครั้งทุกเช้า",
    "query: ログインできない",
    "passage: スプリントのバーンダウンチャートが更新されない問題を修正する",
    "query: SHIP-123",
    "passage: \uff33\uff28\uff29\uff30\uff0d\uff11\uff12\uff13",
    "query:  a\u0308nderung   der  Sprint-Ziele ",
    "passage: var rows = await db.QueryAsync<Item>(\"SELECT * FROM item WHERE id = @id\");",
]

tokenizer = AutoTokenizer.from_pretrained(REPO, revision=REVISION)
cases = [{"text": t, "ids": tokenizer(t, add_special_tokens=True)["input_ids"]} for t in TEXTS]
with open(sys.argv[1], "w", encoding="utf-8") as f:
    json.dump({"model": REPO, "revision": REVISION, "cases": cases}, f, ensure_ascii=False, indent=2)
    f.write("\n")
