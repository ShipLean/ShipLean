#!/usr/bin/env python3
"""The phone app's version: major.minor from git, the patch from the release run.

    python3 -I store/app_version.py           print the version
    python3 -I store/app_version.py --write   also write it into app.json (CI only, never committed)

Major and minor are the first two numbers of `expo.version` in src/ShipLean.Mobile/app.json;
change them there by hand. The patch is APP_VERSION_PATCH when it is set: Mobile release sets it
to github.run_number, the count of Mobile release runs, so every release has a higher version
with no commit. Unset (a local build), the patch in app.json stays.

Exit 1, naming the problem, when app.json's version isn't three numbers or APP_VERSION_PATCH
isn't a whole number.
"""

import json
import os
import re
import sys

APP_JSON = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "src", "ShipLean.Mobile", "app.json")
PATCH_VAR = "APP_VERSION_PATCH"


class VersionError(Exception):
    pass


def version(app, patch):
    current = app["expo"]["version"]
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", current)
    if not match:
        raise VersionError("expo.version in app.json must be major.minor.patch, found {!r}".format(current))
    if patch is None:
        return current
    if not re.fullmatch(r"\d+", patch):
        raise VersionError("{} must be a whole number, found {!r}".format(PATCH_VAR, patch))
    return "{}.{}.{}".format(match.group(1), match.group(2), int(patch))


def main(argv, environ, app_json=APP_JSON):
    write = argv == ["--write"]
    if argv and not write:
        print("usage: store/app_version.py [--write]", file=sys.stderr)
        return 2
    with open(app_json, encoding="utf-8") as handle:
        app = json.load(handle)
    try:
        result = version(app, environ.get(PATCH_VAR) or None)
    except VersionError as error:
        print("app_version.py: {}".format(error), file=sys.stderr)
        return 1
    if write:
        app["expo"]["version"] = result
        with open(app_json, "w", encoding="utf-8") as handle:
            json.dump(app, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
    print(result)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:], os.environ))
