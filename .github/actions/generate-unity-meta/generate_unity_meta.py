#!/usr/bin/env python3
"""Generate stable Unity metadata for a staged UPM package.

Version: 1.0.0

The GUID for each file and folder is UUIDv3(DNS, "<package name>/<relative path>").
That makes metadata reproducible on every release runner while source repositories can
continue to ignore Unity-generated .meta files.
"""

import argparse
import glob
import json
import os
import re
import uuid
from pathlib import Path


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

MONO_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

DEFAULT_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

IGNORED_PATHS = (
    re.compile(r".+\.meta"),
    re.compile(r"\.git/.+"),
    re.compile(r".*/?upm-preparator.*"),
    re.compile(r".*/?temp.*"),
)


def is_ignored(path: str) -> bool:
    return any(pattern.match(path) for pattern in IGNORED_PATHS)


def meta_template(path: str) -> str:
    if not os.path.isfile(path):
        return FOLDER_META

    extension = os.path.splitext(path)[1]
    if extension == ".cs":
        return MONO_META
    if extension in (".json", ".md"):
        return TEXT_META
    if extension == ".asmdef":
        return ASMDEF_META
    return DEFAULT_META


def generate(package_directory: Path) -> None:
    package_file = package_directory / "package.json"
    with package_file.open(encoding="utf-8") as file:
        package_name = json.load(file)["name"]

    current_directory = Path.cwd()
    os.chdir(package_directory)
    try:
        for path in glob.glob("**", recursive=True):
            normalized_path = path.replace("\\", "/")
            if is_ignored(normalized_path):
                continue

            guid = uuid.uuid3(uuid.NAMESPACE_DNS, f"{package_name}/{normalized_path}").hex
            Path(f"{normalized_path}.meta").write_text(
                meta_template(normalized_path).format(guid=guid), encoding="utf-8"
            )
    finally:
        os.chdir(current_directory)


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate deterministic Unity .meta files.")
    parser.add_argument("--package-directory", required=True, type=Path)
    args = parser.parse_args()

    generate(args.package_directory.resolve())


if __name__ == "__main__":
    main()
