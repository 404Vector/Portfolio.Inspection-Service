"""Unit tests for pure helper functions in auto_merge.py."""
import unittest
from auto_merge import strip_code_fences, CONVENTIONAL_COMMIT_RE, parse_approval_from_reviews


class TestStripCodeFences(unittest.TestCase):
    def test_strips_plain_fence(self):
        text = "```\nfeat: add feature\n\n- detail\n```"
        self.assertEqual(strip_code_fences(text), "feat: add feature\n\n- detail")

    def test_strips_named_fence(self):
        text = "```text\nfix: bug\n```"
        self.assertEqual(strip_code_fences(text), "fix: bug")

    def test_no_fence_passthrough(self):
        text = "chore: update deps\n\n- bumped version"
        self.assertEqual(strip_code_fences(text), text)

    def test_empty_string(self):
        self.assertEqual(strip_code_fences(""), "")

    def test_strips_surrounding_whitespace(self):
        text = "  \n```\nfeat: x\n```\n  "
        self.assertEqual(strip_code_fences(text), "feat: x")


class TestConventionalCommitRegex(unittest.TestCase):
    def test_all_valid_types(self):
        for t in ("feat", "fix", "refactor", "chore", "docs", "test", "perf"):
            with self.subTest(type=t):
                self.assertIsNotNone(CONVENTIONAL_COMMIT_RE.match(f"{t}: do something"))

    def test_valid_with_scope(self):
        self.assertIsNotNone(CONVENTIONAL_COMMIT_RE.match("feat(auth): add login"))

    def test_invalid_type(self):
        self.assertIsNone(CONVENTIONAL_COMMIT_RE.match("style: format code"))

    def test_missing_colon_space(self):
        self.assertIsNone(CONVENTIONAL_COMMIT_RE.match("feat add thing"))

    def test_description_too_long(self):
        # description portion exceeds 72 characters
        long_desc = "x" * 73
        self.assertIsNone(CONVENTIONAL_COMMIT_RE.match(f"feat: {long_desc}"))

    def test_description_exactly_72_chars(self):
        desc = "x" * 72
        self.assertIsNotNone(CONVENTIONAL_COMMIT_RE.match(f"feat: {desc}"))

    def test_empty_description(self):
        self.assertIsNone(CONVENTIONAL_COMMIT_RE.match("feat: "))

    def test_no_space_after_colon(self):
        self.assertIsNone(CONVENTIONAL_COMMIT_RE.match("feat:add thing"))


class TestParseApprovalFromReviews(unittest.TestCase):
    @staticmethod
    def _review(login, state):
        return {"user": {"login": login}, "state": state}

    def test_single_approval(self):
        ok, _ = parse_approval_from_reviews([self._review("alice", "APPROVED")])
        self.assertTrue(ok)

    def test_changes_requested_blocks_even_with_approval(self):
        reviews = [
            self._review("alice", "APPROVED"),
            self._review("bob", "CHANGES_REQUESTED"),
        ]
        ok, reason = parse_approval_from_reviews(reviews)
        self.assertFalse(ok)
        self.assertIn("changes", reason.lower())

    def test_no_reviews_is_not_approved(self):
        ok, reason = parse_approval_from_reviews([])
        self.assertFalse(ok)
        self.assertIn("approved", reason.lower())

    def test_only_comment_is_not_approved(self):
        ok, _ = parse_approval_from_reviews([self._review("alice", "COMMENTED")])
        self.assertFalse(ok)

    def test_latest_review_per_reviewer_wins(self):
        # Alice first requested changes, then approved — should count as approved
        reviews = [
            self._review("alice", "CHANGES_REQUESTED"),
            self._review("alice", "APPROVED"),
        ]
        ok, _ = parse_approval_from_reviews(reviews)
        self.assertTrue(ok)

    def test_reviewer_without_user_field_is_ignored(self):
        reviews = [
            {"user": None, "state": "APPROVED"},
            self._review("bob", "APPROVED"),
        ]
        ok, _ = parse_approval_from_reviews(reviews)
        self.assertTrue(ok)

    def test_multiple_approvals(self):
        reviews = [
            self._review("alice", "APPROVED"),
            self._review("bob", "APPROVED"),
        ]
        ok, _ = parse_approval_from_reviews(reviews)
        self.assertTrue(ok)


if __name__ == "__main__":
    unittest.main()
