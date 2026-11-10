"""Tests for store/push_listing.sh (contract C5).

The real script runs with store/tests/fakes first on PATH, so `bundle` is a fake that records
its arguments and environment and exits with $FAKE_BUNDLE_EXIT. These tests pin the exact
fastlane arguments; nobody runs fastlane locally.
"""

import json
import os
import subprocess
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPT = os.path.join("store", "push_listing.sh")
FAKES = os.path.join(REPO, "store", "tests", "fakes")
OWN_VARS = ("ASC_KEY_PATH", "PLAY_KEY_PATH", "STAGED", "FAKE_BUNDLE_EXIT", "FAKE_BUNDLE_LOG",
            "BUNDLE_GEMFILE", "TMPDIR")
UPLOAD_OPTIONS = ("--screenshots_path", "--overwrite_screenshots", "--skip_binary_upload",
                  "--submit_for_review", "--automatic_release", "--app_rating_config_path")


def app_version():
    with open(os.path.join(REPO, "src", "ShipLean.Mobile", "app.json")) as handle:
        return json.load(handle)["expo"]["version"]


class PushListingTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        tmp = self._tmp.name
        self.log = os.path.join(tmp, "bundle.log")
        self.asc_key = os.path.join(tmp, "asc-key.json")
        self.play_key = os.path.join(tmp, "play-key.json")
        for path in (self.asc_key, self.play_key):
            with open(path, "w") as handle:
                handle.write("{}")
        self.staged = os.path.join(tmp, "staged")
        # What stage_images.py writes and the iOS push runs from.
        os.makedirs(os.path.join(self.staged, "ios", "metadata"))

    def tearDown(self):
        self._tmp.cleanup()

    def keys(self):
        return {"ASC_KEY_PATH": self.asc_key, "PLAY_KEY_PATH": self.play_key, "STAGED": self.staged}

    def run_script(self, *args, **env_vars):
        env = {k: v for k, v in os.environ.items() if k not in OWN_VARS}
        env["PATH"] = FAKES + os.pathsep + env.get("PATH", "")
        env["FAKE_BUNDLE_LOG"] = self.log
        env["TMPDIR"] = self._tmp.name
        env.update(env_vars)
        return subprocess.run(
            [SCRIPT] + list(args), cwd=REPO, env=env,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)

    def bundle_call(self):
        """(env, argv) the fake bundle recorded."""
        with open(self.log) as handle:
            lines = handle.read().splitlines()
        env = dict(line.split("=", 1) for line in lines[:2])
        return env, lines[2:]

    def assert_fastlane(self, result, tool, expected_args, pwd=REPO, gemfile="store/Gemfile"):
        self.assertEqual(result.returncode, 0, result.stderr)
        env, argv = self.bundle_call()
        self.assertEqual(env, {"PWD": pwd, "BUNDLE_GEMFILE": gemfile})
        self.assertEqual(argv[:3], ["exec", "fastlane", tool])
        self.assertEqual(argv[3:], expected_args)

    # Mutation caught: drop --overwrite_screenshots.
    # Mutation caught: --metadata_path store/ios/metadata (the text without the en-AU mirror).
    def test_ios_push_passes_the_listing_options(self):
        result = self.run_script("ios", "push", **self.keys())

        self.assert_fastlane(result, "deliver", [
            "--api_key_path", self.asc_key,
            "--app_identifier", "app.shiplean.mobile",
            "--app_version", app_version(),
            "--metadata_path", self.staged + "/ios/metadata",
            "--screenshots_path", self.staged + "/ios/screenshots",
            "--app_rating_config_path", os.path.join(REPO, "store", "ios", "age_rating.json"),
            "--skip_binary_upload", "true",
            "--overwrite_screenshots", "true",
            "--submit_for_review", "false",
            "--automatic_release", "false",
            "--force", "true",
            "--run_precheck_before_submit", "false",
        ], pwd=self.staged + "/ios", gemfile=os.path.join(REPO, "store", "Gemfile"))

    # Mutation caught: run deliver from the repo root, where it has no ./metadata and asks to set
    # itself up, which crashes a non-interactive run (the first Mobile release, 10 Nov 2026).
    def test_ios_push_runs_where_deliver_finds_metadata(self):
        result = self.run_script("ios", "push", **self.keys())

        self.assertEqual(result.returncode, 0, result.stderr)
        env, _ = self.bundle_call()
        self.assertTrue(os.path.isdir(os.path.join(env["PWD"], "metadata")), env["PWD"])

    # Mutation caught: call plain `deliver` instead of `deliver download_metadata`.
    def test_ios_verify_downloads_only(self):
        result = self.run_script("ios", "verify", ASC_KEY_PATH=self.asc_key)

        self.assertEqual(result.returncode, 0, result.stderr)
        _, argv = self.bundle_call()
        self.assertEqual(argv[:4], ["exec", "fastlane", "deliver", "download_metadata"])
        self.assertEqual(argv[4:8], ["--api_key_path", self.asc_key,
                                     "--app_identifier", "app.shiplean.mobile"])
        self.assertEqual(argv[8], "--metadata_path")
        download_dir = argv[9]
        self.assertTrue(os.path.isdir(download_dir), download_dir)
        self.assertEqual(os.path.dirname(download_dir), self._tmp.name)
        self.assertEqual(argv[10:], ["--force", "true"])
        for option in UPLOAD_OPTIONS:
            self.assertNotIn(option, argv)

    def test_android_push_skips_binaries(self):
        result = self.run_script("android", "push", **self.keys())

        self.assert_fastlane(result, "supply", [
            "--json_key", self.play_key,
            "--package_name", "app.shiplean.mobile",
            "--metadata_path", self.staged + "/android/metadata",
            "--skip_upload_aab", "true",
            "--skip_upload_apk", "true",
            "--skip_upload_changelogs", "true",
            "--track", "internal",
        ])

    # Mutation caught: drop --validate_only true.
    def test_android_verify_is_validate_only(self):
        result = self.run_script("android", "verify", PLAY_KEY_PATH=self.play_key, STAGED=self.staged)

        self.assert_fastlane(result, "supply", [
            "--json_key", self.play_key,
            "--package_name", "app.shiplean.mobile",
            "--metadata_path", self.staged + "/android/metadata",
            "--skip_upload_aab", "true",
            "--skip_upload_apk", "true",
            "--skip_upload_changelogs", "true",
            "--track", "internal",
            "--validate_only", "true",
        ])

    # Mutation caught: skip the key (or STAGED) check.
    def test_missing_key_names_the_variable(self):
        missing_file = os.path.join(self._tmp.name, "no-such-key.json")
        cases = [
            ("ios", "push", "ASC_KEY_PATH", ""),
            ("ios", "push", "ASC_KEY_PATH", missing_file),
            ("ios", "verify", "ASC_KEY_PATH", ""),
            ("ios", "verify", "ASC_KEY_PATH", missing_file),
            ("android", "push", "PLAY_KEY_PATH", ""),
            ("android", "push", "PLAY_KEY_PATH", missing_file),
            ("android", "verify", "PLAY_KEY_PATH", ""),
            ("android", "verify", "PLAY_KEY_PATH", missing_file),
            ("ios", "push", "STAGED", ""),
            ("android", "push", "STAGED", ""),
            ("android", "verify", "STAGED", ""),
        ]
        for platform, mode, name, value in cases:
            with self.subTest(platform=platform, mode=mode, name=name, value=value):
                env = self.keys()
                env[name] = value
                result = self.run_script(platform, mode, **env)

                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertIn(name, result.stderr)
                self.assertFalse(os.path.exists(self.log), "fastlane ran")

    # Mutation caught: append `|| true` to the fastlane call.
    def test_fastlane_failure_exit_code_passes_through(self):
        for platform, mode in (("ios", "push"), ("ios", "verify"), ("android", "push"), ("android", "verify")):
            with self.subTest(platform=platform, mode=mode):
                result = self.run_script(platform, mode, FAKE_BUNDLE_EXIT="3", **self.keys())

                self.assertEqual(result.returncode, 3, result.stderr)

    def test_bad_arguments_print_usage(self):
        for args in ([], ["ios"], ["ios", "push", "extra"], ["web", "push"], ["ios", "upload"]):
            with self.subTest(args=args):
                result = self.run_script(*args, **self.keys())

                self.assertEqual(result.returncode, 2)
                self.assertIn("usage: store/push_listing.sh <ios|android> <push|verify>", result.stderr)
                self.assertFalse(os.path.exists(self.log), "fastlane ran")


if __name__ == "__main__":
    unittest.main()
