"""Tests for store/app_version.py: major.minor from app.json, the patch from the release run."""

import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import app_version  # noqa: E402


class AppVersionTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.app_json = os.path.join(self._tmp.name, "app.json")
        self.app = {"expo": {"name": "ShipLean", "version": "1.4.0", "icon": "./assets/ios-icon.png"}}
        self.write_app(self.app)

    def tearDown(self):
        self._tmp.cleanup()

    def write_app(self, app):
        with open(self.app_json, "w", encoding="utf-8") as handle:
            json.dump(app, handle)

    def run_main(self, argv, environ):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = app_version.main(argv, environ, self.app_json)
        return code, out.getvalue().strip(), err.getvalue().strip()

    def read_app(self):
        with open(self.app_json, encoding="utf-8") as handle:
            return json.load(handle)

    def test_patch_comes_from_the_run_number(self):
        self.assertEqual(self.run_main([], {"APP_VERSION_PATCH": "57"}), (0, "1.4.57", ""))

    # Mutation caught: keep app.json's patch when the run sets one.
    def test_without_a_run_number_the_file_version_stays(self):
        self.assertEqual(self.run_main([], {}), (0, "1.4.0", ""))
        self.assertEqual(self.run_main([], {"APP_VERSION_PATCH": ""}), (0, "1.4.0", ""))

    def test_print_leaves_app_json_alone(self):
        self.run_main([], {"APP_VERSION_PATCH": "57"})

        self.assertEqual(self.read_app(), self.app)

    # Mutation caught: --write prints the version but doesn't save it.
    def test_write_sets_only_the_version(self):
        code, out, _ = self.run_main(["--write"], {"APP_VERSION_PATCH": "57"})

        self.assertEqual((code, out), (0, "1.4.57"))
        expected = dict(self.app, expo=dict(self.app["expo"], version="1.4.57"))
        self.assertEqual(self.read_app(), expected)

    def test_leading_zeros_in_the_run_number_are_dropped(self):
        self.assertEqual(self.run_main([], {"APP_VERSION_PATCH": "007"})[1], "1.4.7")

    def test_bad_run_number_names_the_variable(self):
        for patch in ("1.2", "-3", "abc", "5 "):
            code, out, err = self.run_main(["--write"], {"APP_VERSION_PATCH": patch})
            self.assertEqual((code, out), (1, ""), patch)
            self.assertIn("APP_VERSION_PATCH", err)
            self.assertEqual(self.read_app(), self.app)

    def test_version_that_is_not_three_numbers_is_refused(self):
        for bad in ("1.4", "1.4.0-beta", "v1.4.0"):
            self.write_app({"expo": {"version": bad}})
            code, _, err = self.run_main([], {"APP_VERSION_PATCH": "3"})
            self.assertEqual(code, 1, bad)
            self.assertIn("major.minor.patch", err)

    def test_unknown_argument_is_usage(self):
        code, _, err = self.run_main(["--writ"], {})

        self.assertEqual(code, 2)
        self.assertIn("usage", err)

    def test_the_real_app_json_has_a_three_number_version(self):
        with open(app_version.APP_JSON, encoding="utf-8") as handle:
            real = json.load(handle)
        self.assertRegex(app_version.version(real, None), r"^\d+\.\d+\.\d+$")


if __name__ == "__main__":
    unittest.main()
