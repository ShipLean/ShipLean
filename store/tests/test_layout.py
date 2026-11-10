"""Pins contract C1: the listing text in fastlane deliver/supply's own file layout."""
import json
import os
import unittest

STORE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Every listing field and the file fastlane 2.241.2 reads it from.
LISTING_FILES = [
    "ios/metadata/en-US/name.txt",
    "ios/metadata/en-US/subtitle.txt",
    "ios/metadata/en-US/description.txt",
    "ios/metadata/en-US/keywords.txt",
    "ios/metadata/en-US/promotional_text.txt",
    "ios/metadata/en-US/support_url.txt",
    "ios/metadata/en-US/privacy_url.txt",
    "ios/metadata/en-US/marketing_url.txt",
    "ios/metadata/primary_category.txt",
    "ios/metadata/secondary_category.txt",
    "ios/metadata/review_information/notes.txt",
    "ios/age_rating.json",
    "android/metadata/en-US/title.txt",
    "android/metadata/en-US/short_description.txt",
    "android/metadata/en-US/full_description.txt",
    "android/metadata/en-US/changelogs/default.txt",
]

# deliver's review contact and demo login fields (new and legacy file names).
# The repo is public, so these are set by hand in App Store Connect.
REVIEW_CONTACT_FIELDS = (
    "first_name",
    "last_name",
    "phone_number",
    "email_address",
    "review_email",
    "demo_user",
    "demo_password",
)


def _store_files():
    for folder, _, names in os.walk(STORE):
        for name in names:
            yield os.path.join(folder, name)


def _text_files():
    for path in _store_files():
        rel = os.path.relpath(path, STORE)
        if rel.endswith(".txt") and rel.split(os.sep)[0] in ("ios", "android"):
            yield path


class LayoutTests(unittest.TestCase):
    def test_every_listing_field_has_its_file(self):
        for rel in LISTING_FILES:
            path = os.path.join(STORE, rel)
            with self.subTest(file=rel):
                self.assertTrue(os.path.isfile(path), "missing " + rel)
                with open(path, encoding="utf-8") as f:
                    self.assertNotEqual(f.read().strip(), "", "empty " + rel)

    def test_no_review_contact_in_git(self):
        found = [
            os.path.relpath(path, STORE)
            for path in _store_files()
            if any(field in os.path.basename(path) for field in REVIEW_CONTACT_FIELDS)
        ]
        self.assertEqual(found, [])

    def test_text_files_have_no_trailing_whitespace(self):
        # supply sends a file's bytes as they are (deliver strips them), so a
        # trailing newline would count against Play's limits.
        for path in _text_files():
            with self.subTest(file=os.path.relpath(path, STORE)):
                with open(path, encoding="utf-8") as f:
                    text = f.read()
                self.assertEqual(text, text.rstrip())

    def test_age_rating_is_at_the_lowest_answers(self):
        with open(os.path.join(STORE, "ios", "age_rating.json"), encoding="utf-8") as f:
            rating = json.load(f)
        self.assertTrue(rating)
        for key, value in rating.items():
            with self.subTest(key=key):
                self.assertIn(value, ("NONE", False))


if __name__ == "__main__":
    unittest.main()
