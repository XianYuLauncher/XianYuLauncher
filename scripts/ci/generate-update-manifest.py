#!/usr/bin/env python3

import argparse
import hashlib
import json
import re
from pathlib import Path


FEED_PATTERN = re.compile(r"^releases\.(?P<channel>win-(?:x64|x86|arm64)-[A-Za-z0-9]+)\.json$", re.IGNORECASE)
ASSETS_PATTERN = re.compile(r"^assets\.(?P<channel>win-(?:x64|x86|arm64)-[A-Za-z0-9]+)\.json$", re.IGNORECASE)
SETUP_PATTERN = re.compile(r"^(?P<package_id>.+?)-(?P<channel>win-(?:x64|x86|arm64)-[A-Za-z0-9]+)-Setup\.exe$", re.IGNORECASE)
PACKAGE_PATTERN = re.compile(
    r"^(?P<package_id>.+?)-(?P<version>.+)-(?P<channel>win-(?:x64|x86|arm64)-[A-Za-z0-9]+)-(?P<package_kind>full|delta)\.nupkg$",
    re.IGNORECASE,
)
ZIP_PATTERN = re.compile(
    r"^XianYuLauncher(?:_(?P<legacy_version>.+)_(?P<legacy_arch>x64|x86|arm64)|-(?P<tag_version>v.+)-(?P<tag_arch>win-(?:x64|x86|arm64)))\.zip$",
    re.IGNORECASE,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build an indexed release asset inventory for update manifest generation.")
    parser.add_argument("command", choices=["index"], help="Operation to perform.")
    parser.add_argument("--release-assets-dir", required=True, help="Directory containing downloaded release assets.")
    parser.add_argument("--release-channel", required=True, help="Release channel for object key generation, e.g. stable or dev.")
    parser.add_argument("--release-tag", required=True, help="Git tag used for object key generation, e.g. v1.5.4.")
    parser.add_argument("--public-base-url", required=True, help="Public base URL used to compose mirrored asset URLs.")
    parser.add_argument("--output-file", required=True, help="Path to write the generated JSON index.")
    return parser.parse_args()


def sha256_file(file_path: Path) -> str:
    digest = hashlib.sha256()
    with file_path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_asset(file_path: Path, release_channel: str, release_tag: str, public_base_url: str) -> dict | None:
    file_name = file_path.name
    package_type = None
    package_kind = None
    velopack_channel = None
    architecture = None
    package_id = None
    package_version = None

    if match := FEED_PATTERN.match(file_name):
        package_type = "feed"
        velopack_channel = match.group("channel").lower()
    elif match := ASSETS_PATTERN.match(file_name):
        package_type = "assets-manifest"
        velopack_channel = match.group("channel").lower()
    elif match := SETUP_PATTERN.match(file_name):
        package_type = "setup"
        velopack_channel = match.group("channel").lower()
        package_id = match.group("package_id")
    elif match := PACKAGE_PATTERN.match(file_name):
        package_type = "package"
        package_kind = match.group("package_kind").lower()
        velopack_channel = match.group("channel").lower()
        package_id = match.group("package_id")
        package_version = match.group("version")
    elif match := ZIP_PATTERN.match(file_name):
        package_type = "zip"
        architecture = match.group("tag_arch") or match.group("legacy_arch")
        if architecture and not architecture.startswith("win-"):
            architecture = f"win-{architecture.lower()}"
    else:
        return None

    if velopack_channel:
        architecture = velopack_channel.rsplit("-", 1)[0]

    if architecture is None:
        return None

    object_key = f"{release_channel}/{release_tag}/{architecture}/{file_name}"
    public_url = f"{public_base_url.rstrip('/')}/{object_key}"

    asset = {
        "file_name": file_name,
        "file_path": file_path.as_posix(),
        "asset_type": package_type,
        "package_kind": package_kind,
        "architecture": architecture,
        "release_channel": release_channel,
        "release_tag": release_tag,
        "velopack_channel": velopack_channel,
        "object_key": object_key,
        "public_url": public_url,
        "size": file_path.stat().st_size,
        "sha256": sha256_file(file_path),
    }

    if package_id:
        asset["package_id"] = package_id

    if package_version:
        asset["package_version"] = package_version

    return asset


def build_index(release_assets_dir: Path, release_channel: str, release_tag: str, public_base_url: str) -> dict:
    assets = []
    targets: dict[str, dict[str, object]] = {}

    for file_path in sorted(release_assets_dir.iterdir(), key=lambda item: item.name.lower()):
        if not file_path.is_file():
            continue

        asset = parse_asset(file_path, release_channel, release_tag, public_base_url)
        if asset is None:
            continue

        assets.append(asset)

        target = targets.setdefault(
            asset["architecture"],
            {
                "architecture": asset["architecture"],
                "release_channel": release_channel,
                "release_tag": release_tag,
                "files": [],
            },
        )
        target["files"].append(asset)

    return {
        "release_tag": release_tag,
        "release_channel": release_channel,
        "asset_count": len(assets),
        "assets": assets,
        "targets": dict(sorted(targets.items())),
    }


def main() -> int:
    args = parse_args()
    release_assets_dir = Path(args.release_assets_dir)
    if not release_assets_dir.is_dir():
        raise SystemExit(f"Release assets directory not found: {release_assets_dir}")

    if args.command != "index":
        raise SystemExit(f"Unsupported command: {args.command}")

    index_payload = build_index(release_assets_dir, args.release_channel, args.release_tag, args.public_base_url)
    output_file = Path(args.output_file)
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(index_payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())