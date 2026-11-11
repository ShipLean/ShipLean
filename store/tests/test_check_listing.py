"""Tests store/check_listing.py (the Store listing PR check) and the workflows that run it."""
import contextlib
import functools
import importlib.util
import io
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib

STORE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.dirname(STORE)
CHECK = os.path.join(STORE, "check_listing.py")
STAGE = os.path.join(STORE, "stage_images.py")
WORKFLOWS = os.path.join(ROOT, ".github", "workflows")

IOS_FOLDER = "ios/screenshots/en-US"
PLAY_IMAGES = "android/metadata/en-US/images"
# stage_images.py stages each of these as a copy of en-US.
MIRRORED = ["en-AU"]
LOCALE_PARENTS = ["ios/metadata", "ios/screenshots", "android/metadata"]


@functools.lru_cache(maxsize=None)
def png(width, height, alpha=False):
    """A solid PNG, RGB or RGBA."""
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)
    channels = 4 if alpha else 3
    row = b"\x00" + b"\x80" * (width * channels)
    header = struct.pack(">IIBBBBB", width, height, 8, 6 if alpha else 2, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(row * height, 1)) + chunk(b"IEND", b""))


def png_chunks(width, height, color_type=2, extra=(), end=True):
    """A PNG built chunk by chunk: extra is (type, data) chunks placed after IHDR."""
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)
    header = struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)
    out = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
    for kind, data in extra:
        out += chunk(kind, data)
    channels = 4 if color_type == 6 else 3
    out += chunk(b"IDAT", zlib.compress((b"\x00" + b"\x80" * (width * channels)) * height, 1))
    return out + (chunk(b"IEND", b"") if end else b"")


def load_check():
    spec = importlib.util.spec_from_file_location("check_listing", CHECK)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CHECK_MODULE = load_check()


def run_check(*args):
    """Runs check_listing.main in-process (a process per case would take seconds)."""
    args = list(args)
    if "--store" not in args:
        args = ["--store", STORE] + args
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = CHECK_MODULE.main(args)
    return code, out.getvalue()


def run_check_as_cli(*args):
    done = subprocess.run([sys.executable, "-I", CHECK] + list(args),
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True,
                          cwd=ROOT)
    return done.returncode, done.stdout + done.stderr


class TempCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.tmp, True)

    def store_copy(self):
        dest = tempfile.mkdtemp(dir=self.tmp)
        os.rmdir(dest)
        shutil.copytree(STORE, dest, ignore=shutil.ignore_patterns("tests", "__pycache__", "*.py", "*.sh"))
        return dest

    def write(self, root, rel, content):
        path = os.path.join(root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as handle:
            handle.write(content if isinstance(content, bytes) else content.encode("utf-8"))

    def staged_tree(self):
        """A small valid staged tree: the real text, one iOS set of 1 and Play folders of 2,
        with en-US copied to each mirrored locale as stage_images.py does."""
        root = tempfile.mkdtemp(dir=self.tmp)
        for rel in ("ios/metadata", "android/metadata"):
            shutil.copytree(os.path.join(STORE, rel), os.path.join(root, rel))
        self.write(root, IOS_FOLDER + "/iphone-6.9-01-a.png", png(1320, 2868))
        self.write(root, PLAY_IMAGES + "/icon.png", png(512, 512, alpha=True))
        self.write(root, PLAY_IMAGES + "/featureGraphic.png", png(1024, 500))
        for folder in ("phoneScreenshots", "sevenInchScreenshots", "tenInchScreenshots"):
            for number in (1, 2):
                self.write(root, "%s/%s/0%d-a.png" % (PLAY_IMAGES, folder, number), png(540, 960))
        for parent in LOCALE_PARENTS:
            for locale in MIRRORED:
                shutil.copytree(os.path.join(root, parent, "en-US"), os.path.join(root, parent, locale))
        return root


class CheckListingTests(TempCase):
    def assert_bad(self, args, name):
        code, out = run_check(*args)
        self.assertEqual(code, 1, out)
        bad = [line for line in out.splitlines() if line.startswith("BAD")]
        self.assertTrue(any(name in line for line in bad), "no BAD line names %s:\n%s" % (name, out))

    def test_passes_the_real_store_folder(self):
        code, out = run_check()
        self.assertEqual(code, 0, out)
        self.assertNotIn("BAD", out)

    def test_cli_defaults_to_the_store_folder_and_exits_1_on_bad(self):
        code, out = run_check_as_cli()
        self.assertEqual(code, 0, out)
        store = self.store_copy()
        self.write(store, "ios/metadata/en-US/subtitle.txt", "x" * 31)
        code, out = run_check_as_cli("--store", store)
        self.assertEqual(code, 1, out)
        self.assertIn("BAD ios/metadata/en-US/subtitle.txt", out)

    def test_passes_the_real_staged_output(self):
        staged = os.path.join(self.tmp, "real-staged")
        done = subprocess.run([sys.executable, "-I", STAGE, staged], cwd=ROOT,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)
        self.assertEqual(done.returncode, 0, done.stderr)
        code, out = run_check("--staged", staged)
        self.assertEqual(code, 0, out)

    def test_passes_a_minimal_staged_tree(self):
        code, out = run_check("--store", self.store_copy(), "--staged", self.staged_tree())
        self.assertEqual(code, 0, out)

    def test_keywords_limit_counts_bytes(self):
        store = self.store_copy()
        rel = "ios/metadata/en-US/keywords.txt"
        words = "a" * 40 + "," + "b" * 40 + "," + "c" * 15 + "éé"  # 99 characters, 101 bytes
        self.assertEqual(len(words), 99)
        self.assertEqual(len(words.encode("utf-8")), 101)
        self.write(store, rel, words)
        self.assert_bad(["--store", store], "keywords.txt")
        self.write(store, rel, words[:-1])  # 98 characters, 99 bytes: fits
        self.assertEqual(run_check("--store", store)[0], 0)

    def test_flags_each_over_limit_field(self):
        fields = {
            "ios/metadata/en-US/name.txt": 30,
            "ios/metadata/en-US/subtitle.txt": 30,
            "ios/metadata/en-US/promotional_text.txt": 170,
            "ios/metadata/en-US/description.txt": 4000,
            "ios/metadata/en-US/release_notes.txt": 4000,
            "android/metadata/en-US/title.txt": 30,
            "android/metadata/en-US/short_description.txt": 80,
            "android/metadata/en-US/full_description.txt": 4000,
            "android/metadata/en-US/changelogs/default.txt": 500,
            "android/metadata/en-US/changelogs/12.txt": 500,
        }
        for rel, limit in fields.items():
            with self.subTest(rel):
                store = self.store_copy()
                self.write(store, rel, "x" * limit)
                self.assertEqual(run_check("--store", store)[0], 0, "%s at its limit must pass" % rel)
                self.write(store, rel, "x" * (limit + 1))
                self.assert_bad(["--store", store], rel)

    def test_flags_bad_keyword_list(self):
        for label, words in (("space after comma", "one, two"), ("empty entry", "one,,two"),
                             ("trailing comma", "one,two,")):
            with self.subTest(label):
                store = self.store_copy()
                self.write(store, "ios/metadata/en-US/keywords.txt", words)
                self.assert_bad(["--store", store], "keywords.txt")

    def test_flags_a_non_https_url(self):
        for label, url in (("http", "http://shiplean.app"), ("space", "https://shiplean.app/a b"),
                           ("empty", ""), ("bare scheme", "https://")):
            with self.subTest(label):
                store = self.store_copy()
                self.write(store, "ios/metadata/en-US/privacy_url.txt", url)
                self.assert_bad(["--store", store], "privacy_url.txt")

    def test_flags_a_malformed_https_url(self):
        for label, url in (("no host", "https:///path"), ("query only", "https://?query"),
                           ("no dot", "https://localhost/x"), ("two lines", "https://a.app/\nb")):
            with self.subTest(label):
                store = self.store_copy()
                self.write(store, "ios/metadata/en-US/privacy_url.txt", url)
                self.assert_bad(["--store", store], "privacy_url.txt")

    def test_flags_a_missing_url_file(self):
        store = self.store_copy()
        os.remove(os.path.join(store, "ios/metadata/en-US/support_url.txt"))
        self.assert_bad(["--store", store], "support_url.txt")

    def test_flags_a_missing_required_text_file(self):
        for rel in ("android/metadata/en-US/title.txt", "ios/metadata/en-US/name.txt",
                    "ios/metadata/primary_category.txt", "ios/age_rating.json",
                    "android/metadata/en-US/changelogs/default.txt"):
            with self.subTest(rel):
                store = self.store_copy()
                os.remove(os.path.join(store, rel))
                self.assert_bad(["--store", store], rel)

    def staged_with(self, rel, content):
        staged = self.staged_tree()
        self.write(staged, rel, content)
        return ["--store", self.store_copy(), "--staged", staged]

    def test_flags_ios_screenshot_with_alpha(self):
        args = self.staged_with(IOS_FOLDER + "/iphone-6.9-01-a.png", png(1320, 2868, alpha=True))
        self.assert_bad(args, "iphone-6.9-01-a.png")

    def test_flags_wrong_screenshot_size(self):
        args = self.staged_with(IOS_FOLDER + "/iphone-6.9-01-a.png", png(1284, 2778))
        self.assert_bad(args, "iphone-6.9-01-a.png")
        args = self.staged_with(PLAY_IMAGES + "/phoneScreenshots/01-a.png", png(100, 200))
        self.assert_bad(args, "phoneScreenshots/01-a.png")
        args = self.staged_with(PLAY_IMAGES + "/phoneScreenshots/01-a.png", png(500, 1100))
        self.assert_bad(args, "phoneScreenshots/01-a.png")  # long side over twice the short side
        args = self.staged_with(PLAY_IMAGES + "/icon.png", png(256, 256, alpha=True))
        self.assert_bad(args, "icon.png")

    def test_accepts_a_landscape_ios_screenshot(self):
        args = self.staged_with(IOS_FOLDER + "/iphone-6.9-01-a.png", png(2868, 1320))
        self.assertEqual(run_check(*args)[0], 0)

    def test_flags_wrong_feature_graphic_size(self):
        args = self.staged_with(PLAY_IMAGES + "/featureGraphic.png", png(1024, 501))
        self.assert_bad(args, "featureGraphic.png")
        args = self.staged_with(PLAY_IMAGES + "/featureGraphic.png", png(1024, 500, alpha=True))
        self.assert_bad(args, "featureGraphic.png")

    def test_flags_a_feature_graphic_with_trns(self):
        trns = png_chunks(1024, 500, extra=[(b"tRNS", b"\x00\x00\x00\x00\x00\x00")])
        args = self.staged_with(PLAY_IMAGES + "/featureGraphic.png", trns)
        self.assert_bad(args, "featureGraphic.png")

    def test_flags_a_truncated_png(self):
        whole = png(1024, 500)
        for label, data in (("before IEND", whole[:-12]), ("inside IDAT", whole[:60]),
                            ("after IHDR", whole[:33]), ("bad signature", b"x" + whole[1:])):
            with self.subTest(label):
                args = self.staged_with(PLAY_IMAGES + "/featureGraphic.png", data)
                self.assert_bad(args, "featureGraphic.png")

    def test_accepts_a_play_icon_without_alpha(self):
        args = self.staged_with(PLAY_IMAGES + "/icon.png", png(512, 512))
        self.assertEqual(run_check(*args)[0], 0)

    def test_flags_too_many_play_screenshots(self):
        staged = self.staged_tree()
        for number in range(3, 10):
            self.write(staged, "%s/phoneScreenshots/0%d-a.png" % (PLAY_IMAGES, number), png(540, 960))
        self.assert_bad(["--store", self.store_copy(), "--staged", staged], "phoneScreenshots")

    def test_flags_too_few_play_screenshots(self):
        staged = self.staged_tree()
        os.remove(os.path.join(staged, PLAY_IMAGES, "tenInchScreenshots", "02-a.png"))
        self.assert_bad(["--store", self.store_copy(), "--staged", staged], "tenInchScreenshots")

    def test_flags_too_many_ios_screenshots(self):
        staged = self.staged_tree()
        for number in range(2, 12):
            self.write(staged, "%s/iphone-6.9-%02d-a.png" % (IOS_FOLDER, number), png(1320, 2868))
        self.assert_bad(["--store", self.store_copy(), "--staged", staged], "iphone-6.9 set")

    def test_flags_an_ios_name_without_a_known_size(self):
        args = self.staged_with(IOS_FOLDER + "/iphone-5.5-02-a.png", png(1242, 2208))
        self.assert_bad(args, "iphone-5.5-02-a.png")

    def test_flags_en_au_text_that_differs(self):
        # Mutation caught: check_mirrored_text reads the en-US file twice (compares en-US with itself).
        for rel, change in (("ios/metadata/en-AU/name.txt", b"Another name"),
                            ("android/metadata/en-AU/title.txt", b"Another title"),
                            ("android/metadata/en-AU/changelogs/default.txt", None)):
            with self.subTest(rel):
                staged = self.staged_tree()
                if change is None:
                    os.remove(os.path.join(staged, rel))
                else:
                    self.write(staged, rel, change)
                self.assert_bad(["--store", self.store_copy(), "--staged", staged], rel)

    def test_flags_a_missing_mirrored_locale(self):
        # Mutation caught: the missing-folder check loops over [SOURCE_LOCALE] only.
        for parent in LOCALE_PARENTS:
            for locale in MIRRORED:
                with self.subTest(parent=parent, locale=locale):
                    staged = self.staged_tree()
                    shutil.rmtree(os.path.join(staged, parent, locale))
                    self.assert_bad(["--store", self.store_copy(), "--staged", staged],
                                    "%s/%s" % (parent, locale))

    def test_checks_images_in_every_locale_folder(self):
        # Mutation caught: check images in the en-US folders only.
        for rel, data in (("ios/screenshots/en-AU/iphone-6.9-01-a.png", png(1320, 2868, alpha=True)),
                          ("android/metadata/en-AU/images/featureGraphic.png", png(1024, 501))):
            with self.subTest(rel):
                self.assert_bad(self.staged_with(rel, data), rel)

    def test_flags_a_missing_staged_folder(self):
        staged = self.staged_tree()
        shutil.rmtree(os.path.join(staged, PLAY_IMAGES, "sevenInchScreenshots"))
        self.assert_bad(["--store", self.store_copy(), "--staged", staged], "sevenInchScreenshots")


def read_workflow(name):
    with open(os.path.join(WORKFLOWS, name), encoding="utf-8") as handle:
        return handle.read()


def job_block(text, job):
    """The text of one top-level job in a workflow."""
    match = re.search(r"^  %s:\n(.*?)(?=^  [A-Za-z0-9_-]+:\n|\Z)" % re.escape(job), text,
                      re.S | re.M)
    assert match, "no job %s" % job
    return match.group(1)


class StoreListingWorkflowLints(unittest.TestCase):
    def test_actionlint_is_clean(self):
        actionlint = shutil.which("actionlint")
        if actionlint is None:
            self.skipTest("actionlint is not installed here; CI lints workflows separately")
        done = subprocess.run([actionlint, os.path.join(WORKFLOWS, "store-listing.yml"),
                               os.path.join(WORKFLOWS, "ci.yml")],
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, universal_newlines=True)
        self.assertEqual(done.returncode, 0, done.stdout)


class DryRunSkipsWithoutSecrets(unittest.TestCase):
    JOBS = {"verify-android": ("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON", "android"),
            "verify-ios": ("ASC_API_KEY_P8", "ios")}

    def test_each_dry_run_gates_every_step_on_the_secret_check(self):
        text = read_workflow("store-listing.yml")
        for job, (secret, platform) in self.JOBS.items():
            with self.subTest(job):
                block = job_block(text, job)
                self.assertIn("needs: check", block)
                self.assertIn("secrets.%s" % secret, block)
                self.assertNotRegex(block.split("    steps:\n", 1)[0], r"(?m)^    env:",
                                    "no job-level env: secrets go only to the gate and setup")
                self.assertIn("echo \"run=", block)
                self.assertIn("::notice::", block)
                self.assertIn("push_listing.sh %s verify" % platform, block)
                steps = re.split(r"^      - ", block.split("    steps:\n", 1)[1], flags=re.M)[1:]
                self.assertGreaterEqual(len(steps), 4)
                self.assertNotIn("if:", steps[0], "the gate step must always run")
                self.assertIn("run=", steps[0])
                gate_id = re.search(r"id: (\w+)", steps[0]).group(1)
                for step in steps[1:]:
                    self.assertIn("if: steps.%s.outputs.run == 'true'" % gate_id, step)

    def test_secrets_reach_only_the_gate_and_the_setup_action(self):
        text = read_workflow("store-listing.yml")
        for job in self.JOBS:
            with self.subTest(job):
                block = job_block(text, job)
                steps = re.split(r"^      - ", block.split("    steps:\n", 1)[1], flags=re.M)[1:]
                for step in steps:
                    holds = "secrets." in step
                    if step.startswith("id: gate"):
                        self.assertRegex(step, r"(?m)^        env:\n(          \w+: \$\{\{ secrets\.)")
                    elif "uses: ./.github/actions/store-listing-setup" in step:
                        self.assertRegex(step, r"(?m)^        with:\n")
                    else:
                        self.assertFalse(holds, "a step other than the gate and setup sees a secret")
                    self.assertNotRegex(step, r"run: [|>]?-?\n?(?:.*\n)*?.*\$\{\{ secrets\.")

    def test_ios_gate_skips_only_when_all_three_secrets_are_empty(self):
        block = job_block(read_workflow("store-listing.yml"), "verify-ios")
        gate = block.split("      - if:", 1)[0]
        condition = re.search(r"^\s+if (\[ .*? \]); then$", gate, re.M).group(1)
        self.assertEqual(condition.count("-z"), 3, condition)
        self.assertEqual(condition.count("&&"), 2, condition)
        self.assertNotIn("-n", condition)
        self.assertNotIn("||", condition)
        then_branch = gate.split("; then\n", 1)[1].split("else", 1)[0]
        self.assertIn("run=false", then_branch)
        self.assertIn("::notice::", then_branch)

    def test_ios_dry_run_uses_the_ios_runner_variable(self):
        block = job_block(read_workflow("store-listing.yml"), "verify-ios")
        self.assertIn("runs-on: ${{ vars.STORE_IOS_RUNNER || 'ubuntu-latest' }}", block)

    def test_check_job_runs_check_listing_on_the_staged_output(self):
        block = job_block(read_workflow("store-listing.yml"), "check")
        self.assertIn('python3 -I store/check_listing.py --staged "${{ steps.setup.outputs.staged }}"',
                      block)
        self.assertLess(block.index("id: setup"), block.index("check_listing.py"))


class FailuresFailTheCheck(unittest.TestCase):
    def test_nothing_masks_a_failure(self):
        text = read_workflow("store-listing.yml")
        self.assertNotIn("continue-on-error", text)
        self.assertNotIn("|| true", text)
        self.assertNotIn("always()", text)
        self.assertNotIn("set +e", text)


class CiSkipsListingOnlyPullRequests(unittest.TestCase):
    def regex(self):
        text = read_workflow("ci.yml")
        match = re.search(r"grep -Ev '([^']*)'", text)
        self.assertTrue(match)
        return re.compile(match.group(1))

    def test_store_files_skip_and_code_does_not(self):
        pattern = self.regex()
        for path in ("store/README.md", "store/ios/metadata/en-US/name.txt", "store/check_listing.py",
                     "store/assets/exports/play/icon-512.png"):
            self.assertTrue(pattern.match(path), path)
        for path in ("design/mobileapp/app.js", "src/App.cs", "storefront/a.txt", "store.txt"):
            self.assertFalse(pattern.match(path), path)


if __name__ == "__main__":
    unittest.main()
