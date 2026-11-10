"""Pins contract C2: store/stage_images.py builds fastlane's image layout and fails loudly."""
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

STORE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(STORE, "stage_images.py")

# The export names, in caption order (listing.md). Their alphabetical order differs.
CAPTIONS = [
    "1-backlog-listening.png",
    "2-work-item-preview.png",
    "3-inbox-list.png",
    "4-feature-board-board.png",
    "5-work-item-ask.png",
    "6-retro-retro.png",
    "7-search-results.png",
    "8-setup-models.png",
]

IOS_FOLDERS = ["screenshots-6.9", "screenshots-6.5", "screenshots-6.3", "screenshots-ipad-13"]
PLAY_FOLDERS = ["phone-screenshots", "tablet-7-screenshots", "tablet-10-screenshots"]


def _write(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(content)


def _read(path):
    with open(path, "rb") as f:
        return f.read()


def _source_bytes(rel):
    # Each fake export holds its own path, so a copy shows where it came from.
    return b"\x89PNG\r\n\x1a\n" + rel.encode("utf-8")


class StageTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="stage-images-")
        self.addCleanup(shutil.rmtree, self.tmp)
        self.exports = os.path.join(self.tmp, "exports")
        self.out = os.path.join(self.tmp, "out")
        for folder in IOS_FOLDERS:
            for name in CAPTIONS:
                self._export("ios/{}/{}".format(folder, name))
        for folder in PLAY_FOLDERS:
            for name in CAPTIONS:
                self._export("play/{}/{}".format(folder, name))
        self._export("play/icon-512.png")
        self._export("play/feature-graphic.png")

    def _export(self, rel):
        _write(os.path.join(self.exports, rel), _source_bytes(rel))

    def _stage(self):
        return subprocess.run(
            [sys.executable, "-I", SCRIPT, self.out, "--exports", self.exports],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            universal_newlines=True,
        )

    def _staged_ok(self):
        result = self._stage()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(result.stdout.strip().splitlines()), 1, result.stdout)
        return result

    def _assert_fails_naming(self, path):
        result = self._stage()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(path, result.stderr)
        self.assertNotIn("Traceback", result.stderr)
        return result

    def _ios_staged(self):
        return sorted(os.listdir(os.path.join(self.out, "ios", "screenshots", "en-US")))

    def test_stages_ios_iphone_sets_in_caption_order(self):
        self._staged_ok()
        # deliver groups screenshots by pixel size (one set each) and uploads
        # each group in file name order.
        staged = [_read(self._ios_path(name)) for name in self._ios_staged()]
        for size in ("6.9", "6.5", "6.3"):
            folder = "ios/screenshots-{}/".format(size)
            with self.subTest(set=size):
                self.assertEqual(
                    [content for content in staged if folder.encode("utf-8") in content],
                    [_source_bytes(folder + name) for name in CAPTIONS],
                )
        self.assertEqual(len(staged), 24)

    def _ios_path(self, name):
        return os.path.join(self.out, "ios", "screenshots", "en-US", name)

    def test_stages_play_images_and_text(self):
        self._staged_ok()
        metadata = os.path.join(self.out, "android", "metadata")
        images = os.path.join(metadata, "en-US", "images")
        self.assertEqual(_read(os.path.join(images, "icon.png")), _source_bytes("play/icon-512.png"))
        self.assertEqual(
            _read(os.path.join(images, "featureGraphic.png")), _source_bytes("play/feature-graphic.png")
        )
        for source, target in (
            ("phone-screenshots", "phoneScreenshots"),
            ("tablet-7-screenshots", "sevenInchScreenshots"),
            ("tablet-10-screenshots", "tenInchScreenshots"),
        ):
            folder = os.path.join(images, target)
            with self.subTest(folder=target):
                # supply uploads a screenshot folder in file name order.
                self.assertEqual(
                    [_read(os.path.join(folder, name)) for name in sorted(os.listdir(folder))],
                    [_source_bytes("play/{}/{}".format(source, name)) for name in CAPTIONS],
                )
        text = os.path.join(STORE, "android", "metadata")
        for folder, _, names in os.walk(text):
            for name in names:
                path = os.path.join(folder, name)
                rel = os.path.relpath(path, text)
                with self.subTest(text=rel):
                    self.assertEqual(_read(os.path.join(metadata, rel)), _read(path))

    def test_leaves_out_ipad_while_iphone_only(self):
        self._staged_ok()
        staged = [_read(self._ios_path(name)) for name in self._ios_staged()]
        self.assertEqual([content for content in staged if b"ipad" in content], [])
        self.assertEqual(len(staged), 24)

    def test_fails_naming_a_missing_folder(self):
        folder = os.path.join(self.exports, "ios", "screenshots-6.5")
        shutil.rmtree(folder)
        self._assert_fails_naming(folder)
        self.assertFalse(os.path.exists(self.out))

    def test_fails_naming_a_missing_file(self):
        path = os.path.join(self.exports, "play", "feature-graphic.png")
        os.remove(path)
        self._assert_fails_naming(path)
        self.assertFalse(os.path.exists(self.out))

    def test_fails_on_a_wrong_screenshot_count(self):
        folder = os.path.join(self.exports, "play", "tablet-7-screenshots")
        os.remove(os.path.join(folder, CAPTIONS[-1]))
        self._assert_fails_naming(folder)
        self.assertFalse(os.path.exists(self.out))

    def test_fails_on_a_non_png_file(self):
        path = os.path.join(self.exports, "ios", "screenshots-6.3", "notes.txt")
        _write(path, b"not an image")
        self._assert_fails_naming(path)
        self.assertFalse(os.path.exists(self.out))

    def test_refuses_a_non_empty_out_dir(self):
        leftover = os.path.join(self.out, "leftover.txt")
        _write(leftover, b"from an earlier run")
        self._assert_fails_naming(self.out)
        self.assertEqual(os.listdir(self.out), ["leftover.txt"])


if __name__ == "__main__":
    unittest.main()
