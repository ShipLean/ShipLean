#!/usr/bin/env python3
"""Checks the store listing against the stores' limits (SHIP-1060).

Usage: python3 -I store/check_listing.py [--store <dir>] [--staged <dir>]

--store   the listing text folder (default: store)
--staged  the folder store/stage_images.py built; also checks its images in every
          locale folder, and that each mirrored locale (en-AU) is there and its
          text is byte for byte the en-US text

Prints one `ok` or `BAD` line per item and exits 1 when any item is BAD.
Limits are the ones in design/appstore/listing.md.
"""
import argparse
import importlib.util
import os
import struct
import sys
import urllib.parse

DEFAULT_CHANGELOG = "android/metadata/en-US/changelogs/default.txt"

# (file under --store, limit, unit). Every file here is required.
TEXT_LIMITS = [
    ("ios/metadata/en-US/name.txt", 30, "chars"),
    ("ios/metadata/en-US/subtitle.txt", 30, "chars"),
    ("ios/metadata/en-US/keywords.txt", 100, "bytes"),
    ("ios/metadata/en-US/promotional_text.txt", 170, "chars"),
    ("ios/metadata/en-US/description.txt", 4000, "chars"),
    ("android/metadata/en-US/title.txt", 30, "chars"),
    ("android/metadata/en-US/short_description.txt", 80, "chars"),
    ("android/metadata/en-US/full_description.txt", 4000, "chars"),
    (DEFAULT_CHANGELOG, 500, "chars"),
]
# Optional: checked when present.
OPTIONAL_TEXT_LIMITS = [
    ("ios/metadata/en-US/release_notes.txt", 4000, "chars"),
]
CHANGELOGS = "android/metadata/en-US/changelogs"
CHANGELOG_LIMIT = 500
KEYWORDS = "ios/metadata/en-US/keywords.txt"
URL_FILES = [
    "ios/metadata/en-US/support_url.txt",
    "ios/metadata/en-US/privacy_url.txt",
    "ios/metadata/en-US/marketing_url.txt",
]
OTHER_REQUIRED = [
    "ios/metadata/primary_category.txt",
    "ios/metadata/secondary_category.txt",
    "ios/metadata/review_information/notes.txt",
    "ios/age_rating.json",
]

# iOS screenshot sets by the size in the staged name iphone-<size>-<NN>-<caption>.png.
IOS_SIZES = {"6.9": (1320, 2868), "6.5": (1284, 2778), "6.3": (1206, 2622)}
IOS_SET_COUNT = (1, 10)
PLAY_SCREENSHOT_COUNT = (2, 8)
PLAY_SCREENSHOT_SIDE = (320, 3840)
PLAY_SCREENSHOT_MAX_RATIO = 2
PLAY_ICON_SIZE = (512, 512)
PLAY_FEATURE_SIZE = (1024, 500)
PLAY_SCREENSHOT_FOLDERS = ["phoneScreenshots", "sevenInchScreenshots", "tenInchScreenshots"]
IOS_METADATA = "ios/metadata"
IOS_SCREENSHOTS = "ios/screenshots"
ANDROID_METADATA = "android/metadata"


def _locales():
    """stage_images.py's SOURCE_LOCALE and MIRROR_LOCALES: the list is kept there, once."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "stage_images.py")
    spec = importlib.util.spec_from_file_location("stage_images", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.SOURCE_LOCALE, list(module.MIRROR_LOCALES)


SOURCE_LOCALE, MIRROR_LOCALES = _locales()


class Report:
    def __init__(self):
        self.bad = 0

    def ok(self, item, detail=""):
        print("ok  %s%s" % (item, "  " + detail if detail else ""))

    def fail(self, item, reason):
        self.bad += 1
        print("BAD %s: %s" % (item, reason))


def read_text(path):
    with open(path, "rb") as handle:
        return handle.read().decode("utf-8")


def check_limit(report, store, rel, limit, unit):
    path = os.path.join(store, rel)
    if not os.path.isfile(path):
        report.fail(rel, "missing")
        return
    try:
        text = read_text(path)
    except UnicodeDecodeError:
        report.fail(rel, "not UTF-8")
        return
    size = len(text.encode("utf-8")) if unit == "bytes" else len(text)
    if size > limit:
        report.fail(rel, "%d %s, limit %d" % (size, unit, limit))
    else:
        report.ok(rel, "%d/%d %s" % (size, limit, unit))


def check_keywords(report, store):
    path = os.path.join(store, KEYWORDS)
    if not os.path.isfile(path):
        return
    text = read_text(path)
    problems = []
    entries = text.split(",")
    if any(entry == "" for entry in entries):
        problems.append("an empty entry")
    if ", " in text or any(entry != entry.strip() for entry in entries):
        problems.append("a space around a comma")
    if problems:
        report.fail(KEYWORDS, "keywords have " + " and ".join(problems))
    else:
        report.ok(KEYWORDS, "%d keywords" % len(entries))


def check_url(report, store, rel):
    path = os.path.join(store, rel)
    if not os.path.isfile(path):
        report.fail(rel, "missing")
        return
    url = read_text(path)
    if any(ch.isspace() for ch in url):
        report.fail(rel, "URL holds whitespace or more than one line: %r" % url)
        return
    try:
        parts = urllib.parse.urlsplit(url)
        host = parts.hostname or ""
    except ValueError:
        parts, host = None, ""
    if parts is None or parts.scheme != "https" or "." not in host:
        report.fail(rel, "not an https:// URL with a host name: %r" % url)
    else:
        report.ok(rel, url)


def check_text(report, store):
    for rel, limit, unit in TEXT_LIMITS:
        check_limit(report, store, rel, limit, unit)
    for rel, limit, unit in OPTIONAL_TEXT_LIMITS:
        if os.path.isfile(os.path.join(store, rel)):
            check_limit(report, store, rel, limit, unit)
    folder = os.path.join(store, CHANGELOGS)
    if os.path.isdir(folder):
        for name in sorted(os.listdir(folder)):
            rel = CHANGELOGS + "/" + name
            if name.endswith(".txt") and rel != DEFAULT_CHANGELOG:
                check_limit(report, store, rel, CHANGELOG_LIMIT, "chars")
    check_keywords(report, store)
    for rel in URL_FILES:
        check_url(report, store, rel)
    for rel in OTHER_REQUIRED:
        if os.path.isfile(os.path.join(store, rel)):
            report.ok(rel)
        else:
            report.fail(rel, "missing")


def png_info(path):
    """(width, height, has_alpha) from walking the PNG chunks up to IEND.

    has_alpha is true for colour type 4 or 6 and for a tRNS chunk. Raises ValueError
    with the reason when the signature is wrong or the file is cut short. IDAT is not
    decompressed and no CRC is checked.
    """
    with open(path, "rb") as handle:
        data = handle.read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG (bad signature)")
    pos = 8
    size = None
    color_type = None
    trns = False
    while True:
        if pos + 8 > len(data):
            raise ValueError("truncated PNG (no IEND chunk)")
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        end = pos + 8 + length + 4
        if end > len(data):
            raise ValueError("truncated PNG (a %s chunk runs past the end)"
                             % kind.decode("latin-1"))
        if size is None:
            if kind != b"IHDR" or length < 13:
                raise ValueError("not a PNG (no IHDR first)")
            width, height, _depth, color_type = struct.unpack(">IIBB", data[pos + 8:pos + 18])
            size = (width, height)
        elif kind == b"tRNS":
            trns = True
        elif kind == b"IEND":
            return size[0], size[1], color_type in (4, 6) or trns
        pos = end


def pngs_in(folder):
    return sorted(n for n in os.listdir(folder) if n.lower().endswith(".png"))


def check_image(report, staged, rel, sizes, alpha_allowed, side_rule=False):
    """sizes: accepted (w, h) pairs, or None when side_rule decides."""
    try:
        info = png_info(os.path.join(staged, rel))
    except ValueError as problem:
        report.fail(rel, str(problem))
        return
    width, height, has_alpha = info
    label = "%dx%d%s" % (width, height, " alpha" if has_alpha else "")
    if side_rule:
        low, high = PLAY_SCREENSHOT_SIDE
        short, long_ = min(width, height), max(width, height)
        if short < low or long_ > high or long_ > PLAY_SCREENSHOT_MAX_RATIO * short:
            report.fail(rel, "%s: each side %d-%d px and long side at most %dx the short side"
                        % (label, low, high, PLAY_SCREENSHOT_MAX_RATIO))
            return
    elif (width, height) not in sizes:
        want = " or ".join("%dx%d" % size for size in sorted(sizes))
        report.fail(rel, "%s, want %s" % (label, want))
        return
    if has_alpha and not alpha_allowed:
        report.fail(rel, "%s has an alpha channel" % label)
        return
    report.ok(rel, label)


def check_count(report, rel, count, low, high):
    if count < low or count > high:
        report.fail(rel, "%d screenshots, want %d-%d" % (count, low, high))
    else:
        report.ok(rel, "%d screenshots" % count)


def swapped(size):
    return {size, (size[1], size[0])}


def check_ios_images(report, staged, screenshots):
    """screenshots: one locale's folder under the staged tree, e.g. ios/screenshots/en-US."""
    folder = os.path.join(staged, screenshots)
    if not os.path.isdir(folder):
        report.fail(screenshots, "missing")
        return
    names = pngs_in(folder)
    sets = {}
    for name in names:
        rel = screenshots + "/" + name
        parts = name.split("-")
        key = parts[1] if len(parts) > 2 and parts[0] == "iphone" else None
        if key not in IOS_SIZES:
            report.fail(rel, "name must start iphone-<%s>-" % "|".join(sorted(IOS_SIZES)))
            continue
        sets.setdefault(key, []).append(name)
        check_image(report, staged, rel, swapped(IOS_SIZES[key]), alpha_allowed=False)
    for key in sorted(sets):
        check_count(report, "%s (iphone-%s set)" % (screenshots, key),
                    len(sets[key]), *IOS_SET_COUNT)
    if not sets:
        report.fail(screenshots, "no iPhone screenshots")


def check_play_images(report, staged, play_images):
    """play_images: one locale's images folder, e.g. android/metadata/en-US/images."""
    images = os.path.join(staged, play_images)
    if not os.path.isdir(images):
        report.fail(play_images, "missing")
        return
    for name, size, alpha in (("icon.png", PLAY_ICON_SIZE, True),
                              ("featureGraphic.png", PLAY_FEATURE_SIZE, False)):
        rel = play_images + "/" + name
        if os.path.isfile(os.path.join(staged, rel)):
            check_image(report, staged, rel, {size}, alpha_allowed=alpha)
        else:
            report.fail(rel, "missing")
    for folder in PLAY_SCREENSHOT_FOLDERS:
        rel = play_images + "/" + folder
        path = os.path.join(staged, rel)
        if not os.path.isdir(path):
            report.fail(rel, "missing")
            continue
        names = pngs_in(path)
        check_count(report, rel, len(names), *PLAY_SCREENSHOT_COUNT)
        for name in names:
            check_image(report, staged, rel + "/" + name, None, alpha_allowed=False,
                        side_rule=True)


def text_files(folder):
    """The .txt files under folder, as /-separated paths relative to it."""
    found = set()
    for top, _, names in os.walk(folder):
        for name in names:
            if name.endswith(".txt"):
                found.add(os.path.relpath(os.path.join(top, name), folder).replace(os.sep, "/"))
    return found


def check_mirrored_text(report, staged, parent, locale):
    """The text under parent/<locale> must be the text under parent/en-US, byte for byte."""
    source_rel, mirror_rel = parent + "/" + SOURCE_LOCALE, parent + "/" + locale
    source, mirror = os.path.join(staged, source_rel), os.path.join(staged, mirror_rel)
    if not (os.path.isdir(source) and os.path.isdir(mirror)):
        return  # check_staged reports the missing folder.
    bad_before = report.bad
    names = sorted(text_files(source) | text_files(mirror))
    for name in names:
        source_file, mirror_file = os.path.join(source, name), os.path.join(mirror, name)
        rel = mirror_rel + "/" + name
        if not os.path.isfile(mirror_file):
            report.fail(rel, "missing; %s has it" % source_rel)
        elif not os.path.isfile(source_file):
            report.fail(rel, "not in %s" % source_rel)
        else:
            with open(source_file, "rb") as a, open(mirror_file, "rb") as b:
                if a.read() != b.read():
                    report.fail(rel, "differs from %s/%s" % (source_rel, name))
    if report.bad == bad_before:
        report.ok(mirror_rel, "text matches %s (%d files)" % (SOURCE_LOCALE, len(names)))


def locale_folders(staged, parent):
    folder = os.path.join(staged, parent)
    if not os.path.isdir(folder):
        return []
    return sorted(n for n in os.listdir(folder) if os.path.isdir(os.path.join(folder, n)))


def check_staged(report, staged):
    for parent in (IOS_METADATA, IOS_SCREENSHOTS, ANDROID_METADATA):
        for locale in [SOURCE_LOCALE] + MIRROR_LOCALES:
            rel = parent + "/" + locale
            if not os.path.isdir(os.path.join(staged, rel)):
                report.fail(rel, "missing")
    for parent in (IOS_METADATA, ANDROID_METADATA):
        for locale in MIRROR_LOCALES:
            check_mirrored_text(report, staged, parent, locale)
    for locale in locale_folders(staged, IOS_SCREENSHOTS):
        check_ios_images(report, staged, IOS_SCREENSHOTS + "/" + locale)
    for locale in locale_folders(staged, ANDROID_METADATA):
        check_play_images(report, staged, ANDROID_METADATA + "/" + locale + "/images")


def main(argv):
    parser = argparse.ArgumentParser(description="Check the store listing against the store limits.")
    parser.add_argument("--store", default="store", help="the listing text folder")
    parser.add_argument("--staged", help="the folder stage_images.py built")
    args = parser.parse_args(argv)
    report = Report()
    check_text(report, args.store)
    if args.staged:
        check_staged(report, args.staged)
    if report.bad:
        print("%d item(s) BAD" % report.bad)
        return 1
    print("All items ok")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
