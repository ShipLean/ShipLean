"""Tests for store/write_keys.py (contract C4: the key files the setup action writes)."""

import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPT = os.path.join(REPO, "store", "write_keys.py")
INPUTS = ("ASC_KEY_ID", "ASC_ISSUER_ID", "ASC_KEY_P8", "PLAY_JSON")
P8 = "-----BEGIN PRIVATE KEY-----\nMIGTAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBHkwdwIBAQQg\n-----END PRIVATE KEY-----"
PLAY = '{"type": "service_account", "client_email": "ci@example.iam.gserviceaccount.com", "private_key": "pk-secret-value"}'


class WriteKeysTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.out = self._tmp.name

    def tearDown(self):
        self._tmp.cleanup()

    def run_script(self, **inputs):
        env = {k: v for k, v in os.environ.items() if k not in INPUTS}
        env.update(inputs)
        return subprocess.run(
            [sys.executable, "-I", SCRIPT, self.out],
            env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)

    def outputs(self, result):
        return dict(line.split("=", 1) for line in result.stdout.splitlines())

    def assert_private(self, path):
        self.assertEqual(stat.S_IMODE(os.stat(path).st_mode), 0o600)

    # Mutation caught: drop "in_house" from the key JSON.
    def test_writes_asc_key_json_with_all_three(self):
        result = self.run_script(ASC_KEY_ID="KEY123", ASC_ISSUER_ID="issuer-uuid", ASC_KEY_P8=P8)

        self.assertEqual(result.returncode, 0, result.stderr)
        path = os.path.join(self.out, "asc-key.json")
        self.assertEqual(self.outputs(result), {"asc_key_path": path, "play_key_path": ""})
        with open(path) as handle:
            self.assertEqual(json.load(handle), {
                "key_id": "KEY123", "issuer_id": "issuer-uuid", "key": P8, "in_house": False})
        self.assert_private(path)
        self.assertFalse(os.path.exists(os.path.join(self.out, "play-key.json")))

    def test_writes_play_key_json_as_given(self):
        result = self.run_script(PLAY_JSON=PLAY)

        self.assertEqual(result.returncode, 0, result.stderr)
        path = os.path.join(self.out, "play-key.json")
        self.assertEqual(self.outputs(result), {"asc_key_path": "", "play_key_path": path})
        with open(path) as handle:
            self.assertEqual(handle.read(), PLAY)
        self.assert_private(path)

    # Mutation caught: write asc-key.json even when only some ASC inputs are set.
    def test_partial_asc_inputs_fail_naming_each_missing(self):
        result = self.run_script(ASC_KEY_ID="KEY123", ASC_KEY_P8="")

        self.assertEqual(result.returncode, 1)
        self.assertIn("ASC_ISSUER_ID", result.stderr)
        self.assertIn("ASC_KEY_P8", result.stderr)
        self.assertNotIn("ASC_KEY_ID is missing", result.stderr)
        self.assertEqual(os.listdir(self.out), [])
        self.assertEqual(result.stdout, "")

    def test_no_inputs_writes_nothing_and_empty_outputs(self):
        result = self.run_script()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.outputs(result), {"asc_key_path": "", "play_key_path": ""})
        self.assertEqual(os.listdir(self.out), [])

    # Mutation caught: include the PLAY_JSON value in the error message.
    def test_bad_play_json_fails_without_printing_it(self):
        for bad in ('{"type": "service_account", "private_key": "pk-secret-value"}',
                    "not json pk-secret-value"):
            with self.subTest(bad=bad):
                result = self.run_script(PLAY_JSON=bad)

                self.assertEqual(result.returncode, 1)
                self.assertIn("PLAY_JSON", result.stderr)
                self.assertNotIn("pk-secret-value", result.stderr)
                self.assertNotIn("pk-secret-value", result.stdout)
                self.assertEqual(os.listdir(self.out), [])


if __name__ == "__main__":
    unittest.main()
