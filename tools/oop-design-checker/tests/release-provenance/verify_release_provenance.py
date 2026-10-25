"""{"責務": "成果物集合を署名へ渡し、GitHub CLIの暗号検証失敗を公開失敗へ反映します。"}"""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess

from release_artifacts import ReleaseArtifacts


VERIFICATION_TIMEOUT_SECONDS = 90


class ProvenanceVerifier:
    """{"責務": "検証するrepo・workflow・sourceを固定し、全成果物の検証を実行します。", "フィールド": {"repository": "期待repoです。", "source_sha": "期待source commitです。", "source_ref": "期待refです。", "workflow_sha": "期待workflow commitです。"}}"""

    def __init__(self, repository, source_sha, source_ref, workflow_sha):
        """{"責務": "検証identityの欠落や曖昧な入力を拒否します。", "処理": ["repo、commit、refを検証します。"], "引数": {"repository": "owner/repoです。", "source_sha": "source SHAです。", "source_ref": "完全refです。", "workflow_sha": "workflow SHAです。"}, "戻り値": null}"""
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise ValueError("Expected owner/repo")
        if not all(re.fullmatch(r"[0-9a-f]{40}", sha) for sha in (source_sha, workflow_sha)):
            raise ValueError("Expected full commit SHA")
        if not re.fullmatch(r"refs/(?:heads|tags)/[A-Za-z0-9_./-]+", source_ref):
            raise ValueError("Expected branch or tag ref")
        self.repository = repository
        self.source_sha = source_sha
        self.source_ref = source_ref
        self.workflow_sha = workflow_sha

    def verify(self, subjects):
        """{"責務": "全subjectにGitHub CLIの暗号検証とidentity制約を適用します。", "処理": ["署名repo・workflow・source・runnerを固定して順に検証します。"], "引数": {"subjects": "点検済み実ファイルです。"}, "戻り値": null}"""
        if not subjects:
            raise ValueError("No attestation subjects")
        for subject in subjects:
            subprocess.run(
                [
                    "gh",
                    "attestation",
                    "verify",
                    str(subject),
                    "--repo",
                    self.repository,
                    "--signer-workflow",
                    f"{self.repository}/.github/workflows/release.yml",
                    "--signer-digest",
                    self.workflow_sha,
                    "--source-digest",
                    self.source_sha,
                    "--source-ref",
                    self.source_ref,
                    "--deny-self-hosted-runners",
                    "--predicate-type",
                    "https://slsa.dev/provenance/v1",
                ],
                check=True,
                timeout=VERIFICATION_TIMEOUT_SECONDS,
            )


def main(argv=None):
    """{"責務": "prepareまたはverifyを実行し、検査不能を非0終了へ反映します。", "処理": ["入力を解析します。", "実ファイルを点検します。", "署名対象を出力、または暗号検証します。"], "引数": {"argv": "CLI引数です。"}, "戻り値": "成功は0、失敗は1です。"}"""
    parser = argparse.ArgumentParser()
    parser.add_argument("stage", choices=("prepare", "verify"))
    parser.add_argument("--directory", required=True)
    parser.add_argument("--archive", default=None)
    parser.add_argument("--release-version", default=None)
    parser.add_argument("--github-output", type=Path)
    args = parser.parse_args(argv)
    try:
        artifacts = ReleaseArtifacts(
            args.directory, args.archive or None, args.release_version or None
        )
        subjects = artifacts.inspect(create_manifest=args.stage == "prepare")
        if args.stage == "prepare":
            if args.github_output:
                # ファイル名は検証済みです。親ディレクトリ内の改行も出力注入として拒否します。
                if any("\n" in str(path) or "\r" in str(path) for path in subjects):
                    raise ValueError("Artifact paths must not contain newlines")
                with args.github_output.open("a", encoding="utf-8") as output:
                    output.write("subjects<<RELEASE_SUBJECTS\n")
                    output.write("\n".join(map(str, subjects)) + "\nRELEASE_SUBJECTS\n")
            print(json.dumps({"subjects": [str(path) for path in subjects]}))
        else:
            verifier = ProvenanceVerifier(
                os.environ.get("GITHUB_REPOSITORY", ""),
                os.environ.get("GITHUB_SHA", ""),
                os.environ.get("GITHUB_REF", ""),
                os.environ.get("GITHUB_WORKFLOW_SHA", ""),
            )
            verifier.verify(subjects)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Release provenance failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
