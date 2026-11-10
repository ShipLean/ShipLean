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

# Staging copies each en-US folder to these; git holds only en-US.
MIRRORED = ["en-AU"]

IOS_FOLDERS = ["screenshots-6.9", "screenshots-6.5", "screenshots-6.3", "screenshots-ipad-13"]
PLAY_FOLDERS = ["phone-screenshots", "tablet-7-screenshots", "tablet-10-screenshots"]


def _write(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(content)


def _read(path):
    with open(path, "rb") as f:
        return f.read()


def _tree(top):
    """{path relative to top: bytes} for every file under top; empty when top is missing."""
    found = {}
    for folder, _, names in os.walk(top):
        for name in names:
            path = os.path.join(folder, name)
            found[os.path.relpath(path, top)] = _read(path)
    return found


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

    def _stage(self, script=SCRIPT):
        return subprocess.run(
            [sys.executable, "-I", script, self.out, "--exports", self.exports],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            universal_newlines=True,
        )

    def _staged_ok(self):
        result = self._stage()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(result.stdout.strip().splitlines()), 1, result.stdout)
        return result

    def _assert_fails_naming(self, path, script=SCRIPT):
        result = self._stage(script)
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

    def test_mirrors_en_us_to_en_au(self):
        # Mutation caught: MIRROR_LOCALES = [] (nothing is staged under en-AU).
        self._staged_ok()
        for parent in ("ios/metadata", "ios/screenshots", "android/metadata"):
            folder = os.path.join(self.out, parent)
            en_us = _tree(os.path.join(folder, "en-US"))
            for locale in MIRRORED:
                with self.subTest(folder=parent, locale=locale):
                    self.assertEqual(_tree(os.path.join(folder, locale)), en_us)
        # The Android copy holds the images as well as the text.
        android = _tree(os.path.join(self.out, "android", "metadata", "en-AU"))
        self.assertIn(os.path.join("images", "icon.png"), android)
        self.assertIn("title.txt", android)
        self.assertEqual(len([rel for rel in android if "Screenshots" in rel]), 24)
        self.assertEqual(len(os.listdir(os.path.join(self.out, "ios", "screenshots", "en-AU"))), 24)

    def test_stages_ios_text_as_it_is(self):
        # Mutation caught: skip copying store/ios/metadata.
        self._staged_ok()
        staged = _tree(os.path.join(self.out, "ios", "metadata"))
        source = _tree(os.path.join(STORE, "ios", "metadata"))
        self.assertIn("primary_category.txt", source)
        self.assertIn(os.path.join("review_information", "notes.txt"), source)
        self.assertEqual({rel: data for rel, data in staged.items()
                          if rel.split(os.sep)[0] not in MIRRORED}, source)

    def test_en_au_is_not_committed(self):
        # Mutation caught: mkdir store/ios/metadata/en-AU (the text must live once, under en-US).
        committed = [
            os.path.relpath(os.path.join(folder, name), STORE)
            for folder, names, _ in os.walk(STORE)
            for name in names
            if name in MIRRORED
        ]
        self.assertEqual(committed, [])

    def _store_copy(self):
        """A copy of the script and its listing text, for breaking the text without touching git."""
        store = os.path.join(self.tmp, "store")
        for rel in ("ios", "android"):
            shutil.copytree(os.path.join(STORE, rel), os.path.join(store, rel))
        shutil.copyfile(SCRIPT, os.path.join(store, "stage_images.py"))
        return store

    def test_fails_naming_missing_en_us_text(self):
        # Mutation caught: drop the en-US folder check (staging then dies in a traceback).
        store = self._store_copy()
        for platform in ("ios", "android"):
            with self.subTest(platform=platform):
                folder = os.path.join(store, platform, "metadata", "en-US")
                moved = folder + ".moved"
                os.rename(folder, moved)
                self._assert_fails_naming(folder, os.path.join(store, "stage_images.py"))
                self.assertFalse(os.path.exists(self.out))
                os.rename(moved, folder)

    def test_refuses_a_committed_mirror_locale(self):
        # Mutation caught: loop the committed-mirror check over [] (copytree then dies in a traceback).
        store = self._store_copy()
        folder = os.path.join(store, "ios", "metadata", "en-AU")
        shutil.copytree(os.path.join(store, "ios", "metadata", "en-US"), folder)
        self._assert_fails_naming(folder, os.path.join(store, "stage_images.py"))
        self.assertFalse(os.path.exists(self.out))

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

    def test_fails_on_a_duplicate_caption_number(self):
        # Mutation caught: drop the 1-to-8-once-each check (eight PNGs, caption 8 missing, stages).
        folder = os.path.join(self.exports, "ios", "screenshots-6.5")
        os.rename(os.path.join(folder, CAPTIONS[-1]), os.path.join(folder, "1-setup-models.png"))
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
