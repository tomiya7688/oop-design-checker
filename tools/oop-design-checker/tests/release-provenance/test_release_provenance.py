"""{"責務": "成果物の欠落・混入・改変と、暗号検証の失敗を成功扱いしない契約を検証します。"}"""

import contextlib
import hashlib
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from release_artifacts import MANIFEST_NAME, ReleaseArtifacts
from verify_release_provenance import ProvenanceVerifier, main


class ReleaseProvenanceTests(unittest.TestCase):
    """{"責務": "ファイル集合と検証identityの境界を検証します。"}"""

    def setUp(self):
        """{"責務": "各検証専用の成果物ディレクトリを用意します。"}"""
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)

    def write_archive(self, name):
        """{"責務": "テスト用の実バイトと対応するsidecarを用意します。"}"""
        archive = self.directory / name
        archive.write_bytes(b"fixture archive bytes")
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        archive.with_name(name + ".sha256").write_text(
            f"{digest}  {name}\n", encoding="utf-8", newline="\n"
        )
        return archive

    def test_single_package_includes_archive_and_checksum(self):
        """{"責務": "アーカイブ本体だけでなくchecksum自体も署名対象に含めます。"}"""
        self.write_archive("fixture.tar.gz")
        subjects = ReleaseArtifacts(self.directory, archive="fixture.tar.gz").inspect()
        self.assertEqual(
            ["fixture.tar.gz", "fixture.tar.gz.sha256"], [path.name for path in subjects]
        )

    def test_complete_release_requires_twelve_archives_and_manifest(self):
        """{"責務": "CUI/GUI各6RIDとsidecarと統合manifestの25件を検査します。"}"""
        artifacts = ReleaseArtifacts(self.directory, release_version="1.2.3")
        self.assertEqual(12, len(artifacts.archive_names))
        for name in artifacts.archive_names:
            self.write_archive(name)
        subjects = artifacts.inspect(create_manifest=True)
        self.assertEqual(25, len(subjects))
        self.assertIn(MANIFEST_NAME, [path.name for path in subjects])
        self.assertEqual(subjects, artifacts.inspect())

    def test_missing_or_extra_file_fails(self):
        """{"責務": "配布物の欠落と余分なファイルを拒否します。"}"""
        self.write_archive("fixture.zip")
        (self.directory / "unexpected.exe").write_bytes(b"unexpected")
        with self.assertRaisesRegex(ValueError, "missing or unexpected"):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()
        (self.directory / "unexpected.exe").unlink()
        (self.directory / "fixture.zip.sha256").unlink()
        with self.assertRaises(ValueError):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()

    def test_changed_archive_or_checksum_fails(self):
        """{"責務": "本体の改変とchecksumの差替えを検出します。"}"""
        archive = self.write_archive("fixture.zip")
        archive.write_bytes(b"tampered")
        with self.assertRaisesRegex(ValueError, "Checksum mismatch"):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()
        self.write_archive("fixture.zip")
        (self.directory / "fixture.zip.sha256").write_text(
            "0" * 64 + "  fixture.zip\n", encoding="utf-8"
        )
        with self.assertRaises(ValueError):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()

    def test_crlf_sidecar_is_rejected(self):
        """{"責務": "改行を正規化せず、LF以外のsidecarを拒否します。"}"""
        self.write_archive("fixture.zip")
        checksum = self.directory / "fixture.zip.sha256"
        checksum.write_bytes(checksum.read_bytes().replace(b"\n", b"\r\n"))
        with self.assertRaisesRegex(ValueError, "Checksum mismatch"):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()

    def test_crlf_manifest_is_rejected_without_overwriting(self):
        """{"責務": "CRLFの統合manifestを拒否し、元のバイトを保持します。"}"""
        artifacts = ReleaseArtifacts(self.directory, release_version="1.2.3")
        for name in artifacts.archive_names:
            self.write_archive(name)
        artifacts.inspect(create_manifest=True)
        manifest = self.directory / MANIFEST_NAME
        content = manifest.read_bytes().replace(b"\n", b"\r\n")
        manifest.write_bytes(content)
        with self.assertRaisesRegex(ValueError, "manifest mismatch"):
            artifacts.inspect(create_manifest=True)
        self.assertEqual(content, manifest.read_bytes())

    def test_wrong_manifest_is_not_overwritten(self):
        """{"責務": "不正な統合manifestを作り直して成功にしないことを確認します。"}"""
        artifacts = ReleaseArtifacts(self.directory, release_version="1.2.3")
        for name in artifacts.archive_names:
            self.write_archive(name)
        manifest = self.directory / MANIFEST_NAME
        manifest.write_text("tampered", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "manifest mismatch"):
            artifacts.inspect(create_manifest=True)
        self.assertEqual("tampered", manifest.read_text(encoding="utf-8"))

    def test_directories_are_not_subjects(self):
        """{"責務": "期待名を持つディレクトリを通常ファイルとして扱いません。"}"""
        self.write_archive("fixture.zip")
        archive = self.directory / "fixture.zip"
        archive.unlink()
        archive.mkdir()
        with self.assertRaisesRegex(ValueError, "regular files"):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()

    @unittest.skipIf(
        os.name == "nt", "Windows symlink privilege is not required for this unit test"
    )
    def test_symlink_is_rejected(self):
        """{"責務": "外部ファイルを指すリンクを署名対象へ混入させません。"}"""
        archive = self.write_archive("fixture.zip")
        outside = self.directory.parent / (self.directory.name + "-outside")
        outside.write_bytes(archive.read_bytes())
        self.addCleanup(outside.unlink)
        archive.unlink()
        archive.symlink_to(outside)
        with self.assertRaisesRegex(ValueError, "regular files"):
            ReleaseArtifacts(self.directory, archive="fixture.zip").inspect()

    def test_invalid_names_and_modes_fail(self):
        """{"責務": "パスの脱出・改行・曖昧な対象指定を拒否します。"}"""
        for name in ("../fixture.zip", "fixture.zip\nsubjects=injected", "/fixture.zip", "a.exe"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                ReleaseArtifacts(self.directory, archive=name)
        with self.assertRaises(ValueError):
            ReleaseArtifacts(self.directory)
        with self.assertRaises(ValueError):
            ReleaseArtifacts(self.directory, archive="a.zip", release_version="1.2.3")
        with self.assertRaises(ValueError):
            ReleaseArtifacts(self.directory, release_version="../outside")

    def test_verifier_pins_all_identity_constraints(self):
        """{"責務": "repoだけでなくworkflow/source/ref/runnerを暗号検証へ渡します。"}"""
        archive = self.write_archive("fixture.zip")
        verifier = ProvenanceVerifier("owner/repo", "a" * 40, "refs/tags/v1.2.3", "b" * 40)
        with patch("verify_release_provenance.subprocess.run") as run:
            verifier.verify([archive])
        command = run.call_args.args[0]
        for option, expected in {
            "--repo": "owner/repo",
            "--source-digest": "a" * 40,
            "--signer-digest": "b" * 40,
            "--source-ref": "refs/tags/v1.2.3",
            "--signer-workflow": "owner/repo/.github/workflows/release.yml",
        }.items():
            self.assertEqual(expected, command[command.index(option) + 1])
        self.assertIn("--deny-self-hosted-runners", command)
        self.assertEqual({"check": True, "timeout": 90}, run.call_args.kwargs)

    def test_cli_propagates_crypto_failure_and_timeout(self):
        """{"責務": "gh失敗とタイムアウトを非0終了にします。モックは暗号検証の成功証明ではありません。"}"""
        self.write_archive("fixture.zip")
        environment = {
            "GITHUB_REPOSITORY": "owner/repo",
            "GITHUB_SHA": "a" * 40,
            "GITHUB_REF": "refs/tags/v1.2.3",
            "GITHUB_WORKFLOW_SHA": "b" * 40,
        }
        for error in (
            subprocess.CalledProcessError(1, ["gh"]),
            subprocess.TimeoutExpired(["gh"], 90),
            FileNotFoundError("gh"),
        ):
            with (
                patch.dict(os.environ, environment),
                patch("verify_release_provenance.subprocess.run", side_effect=error),
                contextlib.redirect_stdout(io.StringIO()),
            ):
                self.assertEqual(
                    1,
                    main(
                        ["verify", "--directory", str(self.directory), "--archive", "fixture.zip"]
                    ),
                )

    def test_missing_identity_and_empty_subjects_fail(self):
        """{"責務": "identity欠落や検証対象0件を成功にしません。"}"""
        with self.assertRaises(ValueError):
            ProvenanceVerifier("owner/repo", "", "refs/tags/v1", "b" * 40)
        with self.assertRaises(ValueError):
            ProvenanceVerifier("owner/repo", "a" * 40, "", "b" * 40)
        with self.assertRaises(ValueError):
            ProvenanceVerifier("owner/repo", "a" * 40, "refs/tags/v1", "b" * 40).verify([])

    def test_cli_checks_every_subject_on_success(self):
        """{"責務": "verify成功経路で本体とsidecarを両方検証します。"}"""
        self.write_archive("fixture.zip")
        environment = {
            "GITHUB_REPOSITORY": "owner/repo",
            "GITHUB_SHA": "a" * 40,
            "GITHUB_REF": "refs/tags/v1.2.3",
            "GITHUB_WORKFLOW_SHA": "b" * 40,
        }
        with (
            patch.dict(os.environ, environment),
            patch("verify_release_provenance.subprocess.run") as run,
        ):
            self.assertEqual(
                0, main(["verify", "--directory", str(self.directory), "--archive", "fixture.zip"])
            )
        self.assertEqual(2, run.call_count)

    def test_prepare_outputs_exact_paths(self):
        """{"責務": "署名actionへ点検済みsubjectの全件を渡します。"}"""
        self.write_archive("fixture.zip")
        output = self.directory.parent / (self.directory.name + "-output")
        self.addCleanup(lambda: output.unlink(missing_ok=True))
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(
                0,
                main(
                    [
                        "prepare",
                        "--directory",
                        str(self.directory),
                        "--archive",
                        "fixture.zip",
                        "--github-output",
                        str(output),
                    ]
                ),
            )
        content = output.read_text(encoding="utf-8")
        self.assertIn("subjects<<RELEASE_SUBJECTS\n", content)
        self.assertIn(str((self.directory / "fixture.zip.sha256").resolve()), content)


if __name__ == "__main__":
    unittest.main()
