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
MANIFEST_FILE_NAMES = {
    "stable": "update_manifest_stable.json",
    "dev": "update_manifest_dev.json",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build channel-specific bootstrap manifests for SideLoad update delivery.")
    parser.add_argument("command", choices=["index", "manifest"], help="Operation to perform.")
    parser.add_argument("--release-assets-dir", help="Directory containing downloaded release assets.")
    parser.add_argument("--asset-index-file", help="Path to a previously generated asset index JSON file.")
    parser.add_argument("--release-channel", help="Release channel for object key generation, e.g. stable or dev.")
    parser.add_argument("--release-tag", help="Git tag used for object key generation, e.g. v1.5.4.")
    parser.add_argument("--release-version", help="Human-readable release version for the update manifest, e.g. 1.5.4.")
    parser.add_argument("--published-at", help="Published timestamp for the update manifest, ISO 8601.")
    parser.add_argument("--notes-file", help="Text file containing release notes to project into the update manifest.")
    parser.add_argument("--important", action="store_true", help="Mark the generated update manifest as important.")
    parser.add_argument("--strict", action="store_true", help="Fail when required targets or fields are missing.")
    parser.add_argument("--expected-architectures", nargs="*", default=[], help="Architectures that must exist in the generated update manifest when strict mode is enabled.")
    parser.add_argument("--expected-release-channel", help="Expected release channel for strict manifest validation.")
    parser.add_argument("--public-base-url", help="Public base URL used to compose mirrored asset URLs.")
    parser.add_argument("--output-file", help="Path to write the generated JSON output.")
    parser.add_argument("--output-dir", help="Directory for manifest output; fixed stable/dev file names will be derived from the release channel.")
    return parser.parse_args()


def normalize_release_channel(release_channel: str) -> str:
    normalized = release_channel.strip().lower()
    if normalized not in MANIFEST_FILE_NAMES:
        supported_channels = ", ".join(sorted(MANIFEST_FILE_NAMES))
        raise SystemExit(f"Unsupported release channel: {release_channel}. Supported channels: {supported_channels}.")

    return normalized


def resolve_manifest_file_name(release_channel: str) -> str:
    normalized = normalize_release_channel(release_channel)
    return MANIFEST_FILE_NAMES[normalized]


def resolve_output_file(args: argparse.Namespace, release_channel: str | None = None) -> Path:
    if args.output_file and args.output_dir:
        raise SystemExit("--output-file and --output-dir cannot be used together.")

    if args.command == "index":
        if not args.output_file:
            raise SystemExit("index command requires --output-file.")

        return Path(args.output_file)

    if args.output_file:
        return Path(args.output_file)

    if args.output_dir:
        if not release_channel:
            raise SystemExit("manifest command requires a release channel when --output-dir is used.")

        return Path(args.output_dir) / resolve_manifest_file_name(release_channel)

    raise SystemExit("manifest command requires either --output-file or --output-dir.")


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
    release_channel = normalize_release_channel(release_channel)
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
        "manifest_file_name": resolve_manifest_file_name(release_channel),
        "asset_count": len(assets),
        "assets": assets,
        "targets": dict(sorted(targets.items())),
    }


def load_notes(notes_file: Path | None) -> list[str]:
    if notes_file is None or not notes_file.is_file():
        return []

    return [line.strip() for line in notes_file.read_text(encoding="utf-8").splitlines() if line.strip()]


def build_manifest(index_payload: dict, release_tag: str, release_version: str, published_at: str, important: bool, notes: list[str]) -> dict:
    release_channel = normalize_release_channel(index_payload["release_channel"])
    message = "Stable 通道已切换到 Velopack 受管更新。" if release_channel == "stable" else "Dev 通道已使用 Velopack 受管更新。"

    targets = {}
    for architecture, target in index_payload["targets"].items():
        files = target["files"]
        setup_asset = next((item for item in files if item["asset_type"] == "setup"), None)
        feed_asset = next((item for item in files if item["asset_type"] == "feed"), None)
        package_asset = next(
            (item for item in files if item["asset_type"] == "package" and item.get("package_kind") == "full"),
            None,
        )

        if setup_asset is None or feed_asset is None or package_asset is None:
            continue

        targets[architecture] = {
            "channel": feed_asset["velopack_channel"],
            "setup_url": setup_asset["public_url"],
            "setup_sha256": setup_asset["sha256"],
            "feed_url": feed_asset["public_url"],
            "package_url": package_asset["public_url"],
            "package_sha256": package_asset["sha256"],
            "package_size": package_asset["size"],
        }

    return {
        "schema_version": 2,
        "delivery": "velopack",
        "release": {
            "channel": release_channel,
            "tag": release_tag,
            "version": release_version,
            "published_at": published_at,
            "important": important,
        },
        "migration": {
            "required": False,
            "message": message,
        },
        "notes": notes,
        "targets": dict(sorted(targets.items())),
    }


def validate_manifest(manifest_payload: dict, expected_architectures: list[str], expected_release_channel: str | None = None) -> None:
    required_target_fields = (
        "channel",
        "setup_url",
        "setup_sha256",
        "feed_url",
        "package_url",
        "package_sha256",
        "package_size",
    )

    if manifest_payload.get("schema_version") != 2:
        raise SystemExit("Manifest validation failed: schema_version must be 2.")

    if manifest_payload.get("delivery") != "velopack":
        raise SystemExit("Manifest validation failed: delivery must be 'velopack'.")

    release = manifest_payload.get("release", {})
    release_channel = release.get("channel")
    if not release_channel:
        raise SystemExit("Manifest validation failed: release.channel is required.")

    normalize_release_channel(release_channel)

    if expected_release_channel:
        expected_release_channel = normalize_release_channel(expected_release_channel)
        if release_channel != expected_release_channel:
            raise SystemExit(
                f"Manifest validation failed: release.channel must be '{expected_release_channel}', actual '{release_channel}'."
            )

    targets = manifest_payload.get("targets", {})
    missing_architectures = [architecture for architecture in expected_architectures if architecture not in targets]
    if missing_architectures:
        raise SystemExit(
            "Manifest validation failed: missing required targets: " + ", ".join(missing_architectures)
        )

    for architecture, target in targets.items():
        missing_fields = [field for field in required_target_fields if not target.get(field)]
        if missing_fields:
            raise SystemExit(
                f"Manifest validation failed: target {architecture} is missing required fields: {', '.join(missing_fields)}"
            )

        target_channel = target.get("channel")
        if not isinstance(target_channel, str) or not target_channel.endswith(f"-{release_channel}"):
            raise SystemExit(
                f"Manifest validation failed: target {architecture} channel must end with '-{release_channel}'."
            )


def main() -> int:
    args = parse_args()

    if args.command == "index":
        if not args.release_assets_dir or not args.release_channel or not args.release_tag or not args.public_base_url:
            raise SystemExit("index command requires --release-assets-dir, --release-channel, --release-tag and --public-base-url.")

        release_assets_dir = Path(args.release_assets_dir)
        if not release_assets_dir.is_dir():
            raise SystemExit(f"Release assets directory not found: {release_assets_dir}")

        payload = build_index(release_assets_dir, args.release_channel, args.release_tag, args.public_base_url)
        output_file = resolve_output_file(args, payload["release_channel"])
    elif args.command == "manifest":
        if not args.asset_index_file or not args.release_tag or not args.published_at:
            raise SystemExit("manifest command requires --asset-index-file, --release-tag and --published-at.")

        asset_index_file = Path(args.asset_index_file)
        if not asset_index_file.is_file():
            raise SystemExit(f"Asset index file not found: {asset_index_file}")

        index_payload = json.loads(asset_index_file.read_text(encoding="utf-8"))
        release_version = (args.release_version or args.release_tag).removeprefix("v")
        notes = load_notes(Path(args.notes_file) if args.notes_file else None)
        payload = build_manifest(index_payload, args.release_tag, release_version, args.published_at, args.important, notes)
        output_file = resolve_output_file(args, payload["release"]["channel"])
        if args.strict:
            validate_manifest(payload, args.expected_architectures, args.expected_release_channel)
            expected_manifest_file_name = resolve_manifest_file_name(payload["release"]["channel"])
            if output_file.name != expected_manifest_file_name:
                raise SystemExit(
                    f"Manifest validation failed: output file must be '{expected_manifest_file_name}', actual '{output_file.name}'."
                )
    else:
        raise SystemExit(f"Unsupported command: {args.command}")

    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())