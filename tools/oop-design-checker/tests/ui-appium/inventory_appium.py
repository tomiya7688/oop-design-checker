"""{"責務": "Appium依存物の一覧を作り、安全性の判定とは区別します。"}"""

import argparse
import hashlib
import json
import os
from pathlib import Path


NATIVE_SUFFIXES = (".node", ".dll", ".dylib", ".so", ".exe")


def is_link(path):
    """{"責務": "リンクとジャンクションを識別します。", "引数": {"path": "対象パスです。"}, "戻り値": "追跡すべきでないリンクならtrueです。"}"""
    return path.is_symlink() or getattr(path, "is_junction", lambda: False)()


def inventory_tree(label, root):
    """{"責務": "リンクをたどらず依存物を列挙します。", "引数": {"label": "保存する識別名です。", "root": "走査するディレクトリです。"}, "戻り値": "相対パスを使う一覧です。", "エラー": "走査または読取に失敗した場合は例外を伝えます。"}"""
    root = Path(root)
    if not root.is_dir() or is_link(root):
        raise ValueError(f"{label}: inventory root is missing or a symbolic link")
    packages, binaries, skipped_links = [], [], []

    def fail(error):
        """{"責務": "走査失敗を黙殺しません。", "引数": {"error": "発生したエラーです。"}, "戻り値": null}"""
        raise error

    # リンク先をたどると指定範囲外の情報を読むため、名前だけを記録します。
    for directory, subdirs, files in os.walk(root, followlinks=False, onerror=fail):
        base = Path(directory)
        for name in sorted(subdirs + files):
            item = base / name
            relative = item.relative_to(root).as_posix()
            if is_link(item):
                if name in subdirs:
                    subdirs.remove(name)
                skipped_links.append(relative)
            elif (
                item.is_file()
                and name == "package.json"
                and (
                    base == root
                    or base.parent.name == "node_modules"
                    or (
                        base.parent.name.startswith("@")
                        and base.parent.parent.name == "node_modules"
                    )
                )
            ):
                data = json.loads(item.read_text(encoding="utf-8"))
                packages.append(
                    {"path": relative, "name": data.get("name"), "version": data.get("version")}
                )
            elif item.is_file() and name.lower().endswith(NATIVE_SUFFIXES):
                # 同じ読み取りでサイズとハッシュを求め、記録の食い違いを避けます。
                digest = hashlib.sha256()
                size = 0
                with item.open("rb") as stream:
                    for block in iter(lambda: stream.read(1024 * 1024), b""):
                        digest.update(block)
                        size += len(block)
                binaries.append({"path": relative, "bytes": size, "sha256": digest.hexdigest()})
    return {
        "root": label,
        "packages": sorted(packages, key=lambda item: item["path"]),
        "native_files": sorted(binaries, key=lambda item: item["path"]),
        "skipped_links": sorted(skipped_links),
    }


def main():
    """{"責務": "対象を検証し一覧をJSONで保存します。", "引数": {}, "戻り値": null, "副作用": "指定した出力ファイルを書き込みます。"}"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--appium", required=True)
    parser.add_argument("--drivers", required=True)
    parser.add_argument("--driver-package", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    appium = inventory_tree("appium", args.appium)
    drivers = inventory_tree("drivers", args.drivers)
    if not any(
        item["path"] == "package.json" and item["name"] == "appium" for item in appium["packages"]
    ):
        raise ValueError("Appium root package was not found")
    # 同名の推移依存だけでは、対象ドライバーが直下にある証拠になりません。
    if not any(
        item["path"] == f"{args.driver_package}/package.json"
        and item["name"] == args.driver_package
        for item in drivers["packages"]
    ):
        raise ValueError("Expected Appium driver package was not found")
    report = {
        "schema": 1,
        "boundary": "Inventory only; links and bundled libraries require separate investigation.",
        "roots": [appium, drivers],
    }
    Path(args.output).write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
