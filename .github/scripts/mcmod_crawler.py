from __future__ import annotations

import argparse
import base64
import random
import re
import time
from dataclasses import dataclass
from html import unescape
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, Request, build_opener

PLACEHOLDER_LINE = "||||"
DEFAULT_UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/120.0.0.0 Safari/537.36 "
    "XianYuLauncher/1.0 (Gentleman Crawler)"
)


class NoRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


NO_REDIRECT_OPENER = build_opener(NoRedirectHandler())


@dataclass
class ModInfo:
    mod_id: int
    name: str = ""
    en: str = ""
    abbr: str = ""
    cf_id: str = ""
    mr_id: str = ""


def safe_value(value: str) -> str:
    return value.replace("|", "").replace("\r", "").replace("\n", "")


def strip_html(raw: str) -> str:
    return unescape(re.sub(r"<.*?>", "", raw, flags=re.S)).strip()


def parse_html(mod_id: int, html: str) -> ModInfo:
    info = ModInfo(mod_id=mod_id)

    title_match = re.search(r"<h3>(.*?)</h3>", html, flags=re.S)
    if title_match:
        raw = title_match.group(1)
        span_index = raw.lower().find("<span")
        if span_index >= 0:
            raw = raw[:span_index]
        info.name = strip_html(raw)

    en_head_match = re.search(r"<h4>(.*?)</h4>", html, flags=re.S)
    if en_head_match:
        info.en = strip_html(en_head_match.group(1))

    if not info.en:
        en_name_match = re.search(r'class="name-en">(.*?)</span>', html, flags=re.S)
        if en_name_match:
            info.en = strip_html(en_name_match.group(1))

    short_name_match = re.search(r'class="short-name">(.*?)</span>', html, flags=re.S)
    if short_name_match:
        info.abbr = strip_html(short_name_match.group(1)).replace("[", "").replace("]", "").strip()
    else:
        meta_match = re.search(r'<meta name="keywords" content="(.*?)"\s*/?>', html, flags=re.S)
        if meta_match:
            keywords = meta_match.group(1).split(",")
            for keyword in keywords:
                normalized = keyword.strip()
                if (
                    1 < len(normalized) < 8
                    and re.match(r"^[a-zA-Z0-9]+$", normalized)
                    and normalized.lower() not in {"minecraft", "mod", info.en.lower() if info.en else ""}
                ):
                    info.abbr = normalized
                    break

    section_match = re.search(r'class="common-link-icon-frame(.*?)</ul>', html, flags=re.S)
    if section_match:
        links_html = section_match.group(1)
        encoded_links = re.findall(r"link\.mcmod\.cn/target/([a-zA-Z0-9+/=]+)", links_html)

        for encoded_link in encoded_links:
            try:
                real_url = base64.b64decode(encoded_link).decode("utf-8", errors="ignore")
            except Exception:
                continue

            if not info.cf_id:
                cf_match = re.search(
                    r"curseforge\.com/(?:minecraft/mc-mods|projects)/([\w-]+)",
                    real_url,
                    flags=re.I,
                )
                if cf_match:
                    info.cf_id = cf_match.group(1)

            if not info.mr_id:
                mr_match = re.search(r"modrinth\.com/mod/([\w-]+)", real_url, flags=re.I)
                if mr_match:
                    info.mr_id = mr_match.group(1)

    return info


def fetch_html(url: str, user_agent: str, timeout_seconds: float) -> tuple[int, str]:
    request = Request(url, headers={"User-Agent": user_agent})
    try:
        with NO_REDIRECT_OPENER.open(request, timeout=timeout_seconds) as response:
            status_code = response.getcode()
            body = response.read().decode("utf-8", errors="ignore")
            return status_code, body
    except HTTPError as error:
        status_code = error.code
        body = ""
        try:
            body = error.read().decode("utf-8", errors="ignore")
        except Exception:
            pass
        return status_code, body


def ensure_line_ending(file_path: Path) -> None:
    if not file_path.exists() or file_path.stat().st_size == 0:
        return

    with file_path.open("rb") as stream:
        stream.seek(-1, 2)
        last_byte = stream.read(1)

    if last_byte != b"\n":
        with file_path.open("a", encoding="utf-8", newline="") as stream:
            stream.write("\n")


def load_lines(file_path: Path) -> list[str]:
    if not file_path.exists():
        return []
    with file_path.open("r", encoding="utf-8", errors="ignore") as stream:
        return stream.read().splitlines()


def write_lines(file_path: Path, lines: list[str]) -> None:
    with file_path.open("w", encoding="utf-8", newline="") as stream:
        if lines:
            stream.write("\n".join(lines))
            stream.write("\n")


def append_line(file_path: Path, line: str) -> None:
    with file_path.open("a", encoding="utf-8", newline="") as stream:
        stream.write(line)
        stream.write("\n")


def build_line(info: ModInfo) -> str:
    return "|".join(
        [
            safe_value(info.cf_id),
            safe_value(info.mr_id),
            safe_value(info.name),
            safe_value(info.en),
            safe_value(info.abbr),
        ]
    )


def run(args: argparse.Namespace) -> int:
    input_path = Path(args.input).resolve() if args.input else None
    output_path = Path(args.output).resolve()

    output_path.parent.mkdir(parents=True, exist_ok=True)

    existing_lines: list[str] = []
    if input_path and input_path.exists() and not args.full_rebuild:
        ensure_line_ending(input_path)
        existing_lines = load_lines(input_path)

    if args.full_rebuild:
        write_lines(output_path, [])
        current_id = args.start_id
    else:
        write_lines(output_path, existing_lines)
        current_id = max(args.start_id, len(existing_lines) + 1)

    effective_end_id = args.end_id
    if args.max_items and args.max_items > 0:
        effective_end_id = min(effective_end_id, current_id + args.max_items - 1)

    if current_id > effective_end_id:
        print(f"没有需要处理的区间：current={current_id}, end={effective_end_id}")
        print(f"输出文件：{output_path}")
        return 0

    print("=== McMod Python Crawler 启动 ===")
    print(f"区间: {current_id} - {effective_end_id} (原始 end={args.end_id})")
    print(f"输入: {input_path if input_path else '无'}")
    print(f"输出: {output_path}")
    print(f"延迟: {args.delay_min_ms}ms ~ {args.delay_max_ms}ms")

    blocked = False
    for mod_id in range(current_id, effective_end_id + 1):
        url = f"https://www.mcmod.cn/class/{mod_id}.html"
        start_ts = time.time()
        status_label = "OK"

        try:
            status_code, html = fetch_html(url, args.user_agent, args.timeout_seconds)

            if status_code == 404:
                append_line(output_path, PLACEHOLDER_LINE)
                status_label = "404"
                # small polite pause for 404s
                time.sleep(1.0)
            elif status_code == 403:
                append_line(output_path, PLACEHOLDER_LINE)
                status_label = "403"
                blocked = True
            elif status_code < 200 or status_code >= 300:
                append_line(output_path, PLACEHOLDER_LINE)
                status_label = f"HTTP{status_code}"
            else:
                info = parse_html(mod_id, html)
                line = build_line(info)
                append_line(output_path, line)
                status_label = "OK"

        except URLError:
            append_line(output_path, PLACEHOLDER_LINE)
            status_label = "NETERR"
        except Exception:
            append_line(output_path, PLACEHOLDER_LINE)
            status_label = "ERR"

        elapsed_ms = int((time.time() - start_ts) * 1000)
        # Minimal real-time logging: only ID + status + elapsed
        print(f"[{mod_id}] {status_label} {elapsed_ms}ms", flush=True)

        if blocked:
            # stop after printing
            break

        sleep_ms = random.randint(args.delay_min_ms, args.delay_max_ms)
        time.sleep(sleep_ms / 1000.0)

    print("=== 任务结束 ===")
    print(f"输出文件：{output_path}")
    return 2 if blocked else 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="MCMOD 爬虫（Python 版，支持断点续跑）")
    parser.add_argument("--input", help="已存在的 mod_data.txt 路径（可选）")
    parser.add_argument("--output", required=True, help="输出文件路径")
    parser.add_argument("--start-id", type=int, default=1, help="起始 ID")
    parser.add_argument("--end-id", type=int, default=2147483647, help="结束 ID（默认无限上限）")
    parser.add_argument("--max-items", type=int, default=0, help="本次最多处理条数，0 表示不限制")
    parser.add_argument("--delay-min-ms", type=int, default=200, help="最小延迟（毫秒）")
    parser.add_argument("--delay-max-ms", type=int, default=500, help="最大延迟（毫秒）")
    parser.add_argument("--timeout-seconds", type=float, default=15.0, help="单请求超时秒数")
    parser.add_argument("--user-agent", default=DEFAULT_UA, help="请求 User-Agent")
    parser.add_argument("--full-rebuild", action="store_true", help="忽略输入文件，从 start-id 全量重建")

    args = parser.parse_args()
    if args.start_id < 1:
        parser.error("--start-id 必须 >= 1")
    if args.end_id < args.start_id:
        parser.error("--end-id 必须 >= --start-id")
    if args.delay_min_ms < 0 or args.delay_max_ms < 0:
        parser.error("延迟参数必须 >= 0")
    if args.delay_max_ms < args.delay_min_ms:
        parser.error("--delay-max-ms 必须 >= --delay-min-ms")
    return args


if __name__ == "__main__":
    raise SystemExit(run(parse_args()))
