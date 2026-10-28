import io
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from urllib.error import HTTPError, URLError
import update_download_badge as badge


def asset(name, count=1, asset_id=1):
    return {"id": asset_id, "name": name, "download_count": count}


class DownloadBadgeTests(unittest.TestCase):
    def test_only_real_viewer_variants_and_legacy_viewers(self):
        for prefix in ("NxFileViewerFork_v4.2.0-beta1", "NxFileViewer_v4.0.0"):
            for runtime in ("", "_self-contained"):
                for arch in ("x64", "x86"):
                    with self.subTest(prefix=prefix, runtime=runtime, arch=arch):
                        self.assertTrue(badge.is_viewer_zip(f"{prefix}{runtime}_{arch}.zip"))
        for name in (
            "NxFileViewer_v4.1.0_x64.zip",
            "NxFileViewer_v4.1.0-beta1_self-contained_x86.zip",
            "NxFileViewerFork.LogViewer_v4.2.0_x64.zip",
            "NxFileViewerFork.Explorer_1.0.2.0_x64_setup.zip",
            "NxFileViewerFork_v4.2.0_firmware-hashes.zip",
            "nxfileviewer-update.json", "source.zip", "NxFileViewer_x64.zip",
            "NxFileViewerFork_v4.2.0_x64.zip.extra", None,
        ):
            with self.subTest(name=name):
                self.assertFalse(badge.is_viewer_zip(name))

    def test_bad_download_counts_are_not_silently_zero(self):
        for count in (None, -1, "10", True):
            with self.subTest(count=count), self.assertRaises(ValueError):
                badge.asset_count([asset("NxFileViewerFork_v4.2.0_x64.zip", count)])

    def test_all_pages_even_when_server_caps_page_size(self):
        with patch.object(badge, "get_json", side_effect=[[{"id": 1}], [{"id": 2}], []]) as get:
            self.assertEqual([1, 2], [item["id"] for item in badge.pages("https://example.test/releases", {})])
            self.assertEqual(3, get.call_count)
            self.assertIn("page=3", get.call_args.args[0])

    def test_repeated_pages_fail_instead_of_double_counting(self):
        with patch.object(badge, "get_json", side_effect=[[{"id": 1}], [{"id": 1}]]):
            with self.assertRaisesRegex(ValueError, "pagination"):
                list(badge.pages("https://example.test/releases", {}))

    def test_api_error_objects_are_rejected(self):
        with patch.object(badge, "get_json", return_value={"message": "rate limited"}):
            with self.assertRaises(ValueError):
                list(badge.pages("https://example.test/releases", {}))

    def test_github_assets_are_paginated_and_auxiliary_downloads_excluded(self):
        replies = [
            [{"id": 10}],
            [asset("NxFileViewerFork_v4.2.0_x64.zip", 7, 100)],
            [asset("NxFileViewerFork_v4.2.0_self-contained_x86.zip", 3, 101),
             asset("NxFileViewerFork.LogViewer_v4.2.0_x64.zip", 500, 102)],
            [],
            [],
        ]
        with patch.object(badge, "get_json", side_effect=replies):
            self.assertEqual(10, badge.count_source("https://example.test/repo", "github", {}))

    def test_forgejo_assets_endpoint_is_not_repeated_with_unsupported_page_parameter(self):
        replies = [[{"id": 10}], [asset("NxFileViewerFork_v4.2.0_x64.zip", 6)], []]
        with patch.object(badge, "get_json", side_effect=replies) as get:
            self.assertEqual(6, badge.count_source("https://example.test/repo", "forgejo", {}))
            self.assertEqual("https://example.test/repo/releases/10/assets", get.call_args_list[1].args[0])
            self.assertIn("limit=100", get.call_args_list[0].args[0])

    def test_drafts_are_excluded(self):
        with patch.object(badge, "get_json", side_effect=[[{"id": 10, "draft": True}], []]) as get:
            self.assertEqual(0, badge.count_source("https://example.test/repo", "github", {}))
            self.assertEqual(2, get.call_count)

    def test_http_rate_limit_is_retried_with_timeout(self):
        error = HTTPError("https://example.test", 429, "rate limit", {"Retry-After": "0"}, None)
        with patch.object(badge, "urlopen", side_effect=[error, io.BytesIO(b"[]")]) as opening:
            with patch.object(badge.time, "sleep") as sleep:
                self.assertEqual([], badge.get_json("https://example.test", {}))
                sleep.assert_called_once_with(0)
                self.assertEqual(badge.TIMEOUT, opening.call_args.kwargs["timeout"])

    def test_connection_failure_has_bounded_retries(self):
        with patch.object(badge, "urlopen", side_effect=URLError("unreachable")) as opening:
            with patch.object(badge.time, "sleep"):
                with self.assertRaises(RuntimeError):
                    badge.get_json("https://example.test", {})
                self.assertEqual(3, opening.call_count)

    def test_success_writes_badge_but_token_goes_only_to_github(self):
        env = {"GITHUB_REPO": "owner/repo", "FORGEJO_OWNER": "owner",
               "FORGEJO_REPO": "repo", "FORGEJO_URL": "https://forge.example.test/",
               "GITHUB_TOKEN": "test-only-token"}
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, env):
            output = Path(directory) / "downloads.json"
            with patch.object(badge, "count_source", side_effect=[12, 3]) as count:
                badge.generate(output)
                self.assertEqual("15", json.loads(output.read_text())["message"])
                self.assertIn("Authorization", count.call_args_list[0].args[2])
                self.assertNotIn("Authorization", count.call_args_list[1].args[2])
                self.assertFalse(output.with_name("downloads.json.tmp").exists())

    def test_failed_source_retains_existing_badge(self):
        env = {"GITHUB_REPO": "owner/repo", "FORGEJO_OWNER": "owner",
               "FORGEJO_REPO": "repo", "FORGEJO_URL": "https://forge.example.test"}
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, env):
            output = Path(directory) / "downloads.json"
            output.write_bytes(b"previous badge")
            with patch.object(badge, "count_source", side_effect=[12, RuntimeError("unreachable")]):
                with self.assertRaises(RuntimeError):
                    badge.generate(output)
            self.assertEqual(b"previous badge", output.read_bytes())
            self.assertFalse(output.with_name("downloads.json.tmp").exists())


if __name__ == "__main__":
    unittest.main()
