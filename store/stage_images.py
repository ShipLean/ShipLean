"""Copy the store images from design/appstore/exports into fastlane's layout (contract C2).

    python3 -I store/stage_images.py <out> [--exports <dir>]

Builds <out>/ios/screenshots/en-US/ for deliver and <out>/android/metadata/
(a copy of store/android/metadata plus en-US/images/) for supply. Files are
copied byte for byte, never edited. Every source is checked before anything is
copied; on a problem it exits 1 naming the path on stderr.
"""
import argparse
import os
import re
import shutil
import sys

STORE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_EXPORTS = os.path.join(os.path.dirname(STORE), "design", "appstore", "exports")
ANDROID_METADATA = os.path.join(STORE, "android", "metadata")

SCREENSHOTS_PER_SET = 8

# iPhone sets deliver 2.241.2 recognises by pixel size (deliver/lib/deliver/app_screenshot.rb):
#   screenshots-6.9  1320 x 2868  APP_IPHONE_67 (fastlane's name for the 6.9" class)
#   screenshots-6.5  1284 x 2778  APP_IPHONE_65
#   screenshots-6.3  1206 x 2622  APP_IPHONE_61 (fastlane's name for the 6.3" class)
# All three are known, so none is left out for its size.
# TODO: stage ios/screenshots-ipad-13 once the app supports tablets.
IOS_SETS = ["6.9", "6.5", "6.3"]

# Source folder under play/ -> supply's screenshot folder.
PLAY_SCREENSHOTS = [
    ("phone-screenshots", "phoneScreenshots"),
    ("tablet-7-screenshots", "sevenInchScreenshots"),
    ("tablet-10-screenshots", "tenInchScreenshots"),
]

# Source file under play/ -> supply's image file.
PLAY_IMAGES = [
    ("icon-512.png", "icon.png"),
    ("feature-graphic.png", "featureGraphic.png"),
]

# Export names start with their caption number: 1-backlog-listening.png.
CAPTION_NAME = re.compile(r"^(\d+)-(.+\.png)$")


class StageError(Exception):
    pass


def _caption_order(folder):
    """The folder's screenshots as (caption number, name without the number, path), in caption order."""
    if not os.path.isdir(folder):
        raise StageError("missing folder: " + folder)
    names = sorted(os.listdir(folder))
    for name in names:
        path = os.path.join(folder, name)
        if not name.endswith(".png") or not os.path.isfile(path):
            raise StageError("not a .png file: " + path)
    if len(names) != SCREENSHOTS_PER_SET:
        raise StageError(
            "expected {} PNGs, found {}: {}".format(SCREENSHOTS_PER_SET, len(names), folder)
        )
    shots = []
    for name in names:
        match = CAPTION_NAME.match(name)
        if not match:
            raise StageError("name does not start with its caption number: " + os.path.join(folder, name))
        shots.append((int(match.group(1)), match.group(2), os.path.join(folder, name)))
    numbers = sorted(number for number, _, _ in shots)
    if numbers != list(range(1, SCREENSHOTS_PER_SET + 1)):
        raise StageError(
            "caption numbers must be 1 to {} once each, found {}: {}".format(
                SCREENSHOTS_PER_SET, numbers, folder
            )
        )
    return sorted(shots)


def _require_file(path):
    if not os.path.isfile(path):
        raise StageError("missing file: " + path)
    return path


def _require_empty(out):
    if os.path.exists(out) and (not os.path.isdir(out) or os.listdir(out)):
        raise StageError("output folder is not empty: " + out)


def plan(exports, out):
    """Check every source and return the iOS and the Play (source, destination) copies, or raise StageError."""
    _require_empty(out)
    if not os.path.isdir(ANDROID_METADATA):
        raise StageError("missing folder: " + ANDROID_METADATA)

    ios = []
    ios_out = os.path.join(out, "ios", "screenshots", "en-US")
    for size in IOS_SETS:
        folder = os.path.join(exports, "ios", "screenshots-" + size)
        for number, rest, src in _caption_order(folder):
            # deliver sorts by file name within a device size; the set prefix keeps sets apart.
            name = "iphone-{}-{:02d}-{}".format(size, number, rest)
            ios.append((src, os.path.join(ios_out, name)))

    play = []
    images_out = os.path.join(out, "android", "metadata", "en-US", "images")
    for src_name, dest_name in PLAY_IMAGES:
        src = _require_file(os.path.join(exports, "play", src_name))
        play.append((src, os.path.join(images_out, dest_name)))
    for src_folder, dest_folder in PLAY_SCREENSHOTS:
        folder = os.path.join(exports, "play", src_folder)
        for number, rest, src in _caption_order(folder):
            name = "{:02d}-{}".format(number, rest)
            play.append((src, os.path.join(images_out, dest_folder, name)))
    return ios, play


def stage(exports, out):
    ios, play = plan(exports, out)
    shutil.copytree(ANDROID_METADATA, os.path.join(out, "android", "metadata"))
    for src, dest in ios + play:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(src, dest)
    return "Staged {} iOS screenshots (iPhone {}) and {} Play images into {}".format(
        len(ios), ", ".join(IOS_SETS), len(play), out
    )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("out", help="empty or missing folder to build fastlane's layout in")
    parser.add_argument("--exports", default=DEFAULT_EXPORTS, help="the design/appstore/exports folder")
    args = parser.parse_args(argv)
    try:
        summary = stage(os.path.abspath(args.exports), os.path.abspath(args.out))
    except StageError as error:
        print("stage_images: " + str(error), file=sys.stderr)
        return 1
    print(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())
