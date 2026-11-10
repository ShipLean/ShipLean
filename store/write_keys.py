"""Write the store key files for store/push_listing.sh (contract C4).

Usage: python3 -I store/write_keys.py <out-dir>

Reads ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8 and PLAY_JSON from the environment (an empty
value counts as unset). Writes, mode 600:

- <out-dir>/asc-key.json, the App Store Connect API key JSON deliver's api_key_path reads,
  only when all three ASC values are set. Exit 1 naming each missing one when only some are.
- <out-dir>/play-key.json, the Play service account JSON supply's json_key reads, when
  PLAY_JSON is set. Exit 1 when it isn't a JSON object with a client_email.

Nothing is written when any input is wrong. On success it prints two lines for $GITHUB_OUTPUT:
asc_key_path=<path or empty> and play_key_path=<path or empty>. It never prints a key value.
"""

import json
import os
import sys

ASC_VARS = ("ASC_KEY_ID", "ASC_ISSUER_ID", "ASC_KEY_P8")
ASC_FILE = "asc-key.json"
PLAY_FILE = "play-key.json"


def _env(name):
    return os.environ.get(name, "")


def _is_service_account(text):
    try:
        value = json.loads(text)
    except ValueError:
        return False
    return isinstance(value, dict) and bool(value.get("client_email"))


def _write_private(path, text):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    # The open mode only applies to a new file; tighten an existing one before writing the secret.
    os.fchmod(fd, 0o600)
    with os.fdopen(fd, "w") as handle:
        handle.write(text)


def main(argv):
    if len(argv) != 1:
        print("usage: python3 -I store/write_keys.py <out-dir>", file=sys.stderr)
        return 2
    out_dir = os.path.abspath(argv[0])
    if not os.path.isdir(out_dir):
        print("write_keys.py: {} is not a directory".format(out_dir), file=sys.stderr)
        return 1

    errors = []
    asc_set = [name for name in ASC_VARS if _env(name)]
    if asc_set and len(asc_set) != len(ASC_VARS):
        for name in ASC_VARS:
            if not _env(name):
                errors.append("{} is missing: the App Store key needs all of {}".format(
                    name, ", ".join(ASC_VARS)))
    play_json = _env("PLAY_JSON")
    if play_json and not _is_service_account(play_json):
        errors.append("PLAY_JSON is not a service account key (a JSON object with a client_email)")
    if errors:
        for error in errors:
            print("write_keys.py: " + error, file=sys.stderr)
        return 1

    asc_path = ""
    if asc_set:
        asc_path = os.path.join(out_dir, ASC_FILE)
        _write_private(asc_path, json.dumps({
            "key_id": _env("ASC_KEY_ID"),
            "issuer_id": _env("ASC_ISSUER_ID"),
            "key": _env("ASC_KEY_P8"),
            "in_house": False,
        }))
    play_path = ""
    if play_json:
        play_path = os.path.join(out_dir, PLAY_FILE)
        _write_private(play_path, play_json)

    print("asc_key_path=" + asc_path)
    print("play_key_path=" + play_path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
