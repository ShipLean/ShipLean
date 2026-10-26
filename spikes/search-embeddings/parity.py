#!/usr/bin/env python
"""Tokenizer parity: the C# XlmrTokenizer (via `--ranker tokenize`) against transformers' tokenizer for each model.

    ~/.cache/shiplean/search-venv/bin/python spikes/search-embeddings/parity.py [--max-tokens 512]

Each case is (name, directory the C# side loads sentencepiece.bpe.model from, directory with the transformers tokenizer files).
The int8 production directory has no tokenizer.json, so transformers reads the fp32 directory (same repo and revision).
Exits 1 when any text differs.
"""
import argparse
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

from transformers import AutoTokenizer

MODELS = Path.home() / ".cache/shiplean/models"
CASES = [
    ("e5-int8", MODELS / "multilingual-e5-small", MODELS / "spike/e5-small-fp32"),
    ("e5-fp32", MODELS / "spike/e5-small-fp32", MODELS / "spike/e5-small-fp32"),
    ("minilm", MODELS / "spike/minilm-l12-multilingual", MODELS / "spike/minilm-l12-multilingual"),
]
TEXTS = [
    "query: login broken",
    "passage: Sign-in fails after password reset\nUsers who reset their password are bounced back to the sign-in page.",
    "SHIP-12 checkout totals drift by a cent (v2.3.1-rc.4)",
    "Der Warenkorb wird nach dem Abmelden geleert, Größenänderung ärgert Kunden",
    "El carrito no guarda los artículos después de cerrar sesión; ¿qué pasó?",
    "ログインできない問題と、パスワード再設定後のエラー",
    "ไม่สามารถเข้าสู่ระบบได้หลังจากรีเซ็ตรหัสผ่าน",
    "  odd   spacing,\ttabs and ‘smart quotes’ — plus emoji 🚀 and zero​width  ",
    "a rareword like Zyxwvutsrq and CamelCaseIdentifier_with_underscores",
    "passage: " + " ".join(f"Row {i} of the nightly export mismatches the ledger total." for i in range(120)),
]


def csharp_ids(model_dir: Path, max_tokens: int) -> list[list[int]]:
    root = Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory() as tmp:
        texts = Path(tmp) / "texts.json"
        texts.write_text(json.dumps(TEXTS))
        cmd = ["dotnet", "run", "--project", str(root / "spikes/search-embeddings"), "--", "--ranker", "tokenize",
               "--inputs", str(texts), "--model-dir", str(model_dir), "--chunk-size", str(max_tokens), "--out", str(Path(tmp) / "unused.json")]
        subprocess.run(["flock", "/tmp/shiplean-test.lock", "nice", "-n", "10", "timeout", "300", *cmd], check=True, stdout=subprocess.DEVNULL, cwd=root)
        return json.loads(Path(str(texts) + ".ids.json").read_text())


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--max-tokens", type=int, default=512)
    args = parser.parse_args()
    failed = False
    for name, csharp_dir, hf_dir in CASES:
        tokenizer = AutoTokenizer.from_pretrained(str(hf_dir))
        expected = [tokenizer(t, truncation=True, max_length=args.max_tokens)["input_ids"] for t in TEXTS]
        actual = csharp_ids(csharp_dir, args.max_tokens)
        same = sum(1 for a, b in zip(expected, actual) if a == b)
        print(f"{name}: {same}/{len(TEXTS)} identical ids")
        for i, (a, b) in enumerate(zip(expected, actual)):
            if a != b:
                failed = True
                first = next((k for k, (x, y) in enumerate(zip(a, b)) if x != y), min(len(a), len(b)))
                print(f"  text {i} differs at token {first}: transformers {a[first:first + 5]} C# {b[first:first + 5]} (lengths {len(a)} vs {len(b)})")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
