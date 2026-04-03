import os
import sys
import subprocess
import json
import re
import argparse

GITHUB_NOREPLY_EMAIL_PATTERNS = (
    re.compile(r"^\d+\+([A-Za-z0-9-]+)@users\.noreply\.github\.com$", re.IGNORECASE),
    re.compile(r"^([A-Za-z0-9-]+)@users\.noreply\.github\.com$", re.IGNORECASE),
)

def infer_github_login_from_email(author_email):
    """从 GitHub noreply 邮箱推断 login。"""
    if not author_email:
        return None

    normalized_email = author_email.strip()
    for pattern in GITHUB_NOREPLY_EMAIL_PATTERNS:
        match = pattern.match(normalized_email)
        if match:
            return match.group(1)

    return None

def get_github_commit_author_login(repo, github_token, commit_hash, cache):
    """从 GitHub Commit API 获取 canonical author login。"""
    if not repo or '/' not in repo or not github_token or not commit_hash:
        return None

    if commit_hash in cache:
        return cache[commit_hash]

    try:
        import requests

        owner, name = repo.split('/', 1)
        headers = {
            "Authorization": f"Bearer {github_token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28"
        }
        response = requests.get(
            f"https://api.github.com/repos/{owner}/{name}/commits/{commit_hash}",
            headers=headers,
            timeout=60)

        if response.status_code != 200:
            print(f"GitHub commit lookup error for {commit_hash}: {response.status_code} - {response.text}")
            cache[commit_hash] = None
            return None

        result = response.json()
        author = result.get('author') or {}
        login = author.get('login')
        cache[commit_hash] = login.strip() if isinstance(login, str) and login.strip() else None
        return cache[commit_hash]
    except Exception as e:
        print(f"Exception getting GitHub commit author for {commit_hash}: {e}")
        cache[commit_hash] = None
        return None

def resolve_canonical_contributor(author_name, author_email, repo, github_token, commit_hash, cache):
    """解析 commit 的 canonical contributor，优先使用 GitHub login。"""
    github_login = get_github_commit_author_login(repo, github_token, commit_hash, cache)
    if github_login:
        return f"@{github_login}", "github-commit-author"

    inferred_login = infer_github_login_from_email(author_email)
    if inferred_login:
        return f"@{inferred_login}", "github-noreply-email"

    if author_name and author_name.strip():
        return author_name.strip(), "git-author-name"

    return "unknown", "unknown"

def get_git_commits(last_tag, current_ref, repo='', github_token=''):
    """
    获取详细的提交记录，包含 Message, Stat 和部分 Diff
    """
    try:
        # 1. 获取 Commit Hash 列表
        range_spec = f"{last_tag}..{current_ref}" if last_tag else current_ref
        cmd = ["git", "log", "--pretty=format:%H", range_spec]
        result = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8')
        hashes = result.stdout.strip().splitlines()
        
        output_buffer = []
        commit_author_cache = {}
        MAX_DIFF_LEN = 1500  # 每个 Commit 最多读取多少字符的 Diff，防止 Token 爆炸
        
        print(f"Analyzing {len(hashes)} commits for diffs...")
        
        # 倒序处理（从旧到新），或者正序都可以，git log 默认是新到旧
        for commit_hash in hashes:
            if not commit_hash: continue

            meta_cmd = ["git", "show", "-s", "--format=%h%x1f%an%x1f%ae%x1f%cd%x1f%s%x1f%b", commit_hash]
            meta_output = subprocess.run(meta_cmd, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
            short_hash, author_name, author_email, commit_date, subject, body = (meta_output.split("\x1f", 5) + [""] * 6)[:6]
            author_name = author_name.strip()
            author_email = author_email.strip()
            commit_date = commit_date.strip()
            subject = subject.strip()
            body = body.strip()

            canonical_contributor, contributor_source = resolve_canonical_contributor(
                author_name,
                author_email,
                repo,
                github_token,
                commit_hash,
                commit_author_cache)

            info_lines = [
                f"Commit: {short_hash.strip()}",
                f"Author Name: {author_name}",
                f"Author Email: {author_email}",
                f"Canonical Contributor: {canonical_contributor}",
                f"Canonical Contributor Source: {contributor_source}",
                f"Date: {commit_date}",
                f"Message: {subject}"
            ]
            if body:
                info_lines.append(body)
            info = "\n".join(info_lines)
            
            # 获取修改的文件列表 (Stat)
            stat_cmd = ["git", "show", "--stat", "--oneline", "--no-patch", commit_hash]
            stat = subprocess.run(stat_cmd, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout.strip()
            # 移除第一行（因为它包含了 commit message，我们上面已经有了）
            stat_lines = stat.splitlines()[1:] 
            stat_clean = "\n".join(stat_lines)

            # 获取具体的 Diff (裁剪)
            diff_cmd = ["git", "show", "--patch", "--minimal", commit_hash]
            diff_proc = subprocess.run(diff_cmd, capture_output=True, text=True, encoding='utf-8', errors='replace')
            diff_full = diff_proc.stdout
            
            # 智能截断：保留头部信息，中间切断
            diff_content = ""
            if len(diff_full) > MAX_DIFF_LEN:
                diff_content = diff_full[:MAX_DIFF_LEN] + "\n... (Diff truncated due to length) ..."
            else:
                diff_content = diff_full

            # 组装 Prompt 块
            block = f"""
================================================
{info}
[Changed Files]
{stat_clean}

[Diff Summary]
{diff_content}
================================================
"""
            output_buffer.append(block)
            
        return "\n".join(output_buffer)
        
    except Exception as e:
        print(f"Exception getting git details: {e}")
        return ""

def get_previous_tag(current_tag):
    """尝试获取上一个 Tag，正式版跳过所有 dev/beta tag"""
    try:
        # 获取按时间排序的所有 tag
        cmd = ["git", "tag", "--sort=-creatordate"]
        result = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8')
        tags = result.stdout.strip().split('\n')
        
        # 简单过滤掉空行
        tags = [t for t in tags if t]
        
        if not tags:
            return None
        
        # 判断当前 tag 是否为正式版（不包含 -dev, -beta 等后缀）
        is_stable = current_tag and '-' not in current_tag
        
        # 如果当前 tag 在列表中，找它后面那个
        if current_tag in tags:
            idx = tags.index(current_tag)
            
            # 如果是正式版，跳过所有 dev/beta tag，找上一个正式版
            if is_stable:
                for i in range(idx + 1, len(tags)):
                    if '-' not in tags[i]:  # 找到第一个不含 - 的 tag（正式版）
                        return tags[i]
                return None  # 没找到上一个正式版
            else:
                # dev/beta 版本，直接返回下一个 tag
                if idx + 1 < len(tags):
                    return tags[idx + 1]
        
        # 如果找不到或者当前 tag 是最新的且列表里有其他 tag
        if len(tags) > 1 and tags[0] == current_tag:
            if is_stable:
                # 正式版，找第一个不含 - 的
                for tag in tags[1:]:
                    if '-' not in tag:
                        return tag
                return None
            else:
                return tags[1]
        elif len(tags) > 0 and tags[0] != current_tag:
             return tags[0] 
             
        return None
    except Exception:
        return None

def get_github_generated_release_notes(repo, github_token, current_tag, previous_tag=None):
    """获取 GitHub 内置生成的 Release Notes。"""
    if not repo or '/' not in repo or not github_token or not current_tag:
        return None

    try:
        import requests

        owner, name = repo.split('/', 1)
        headers = {
            "Authorization": f"Bearer {github_token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28"
        }
        payload = {
            "tag_name": current_tag,
            "target_commitish": current_tag
        }
        if previous_tag:
            payload["previous_tag_name"] = previous_tag

        response = requests.post(
            f"https://api.github.com/repos/{owner}/{name}/releases/generate-notes",
            headers=headers,
            json=payload,
            timeout=60)

        if response.status_code != 200:
            print(f"GitHub generate-notes error: {response.status_code} - {response.text}")
            return None

        result = response.json()
        body = result.get('body')
        return body.strip() if body else None
    except Exception as e:
        print(f"Exception getting GitHub generated release notes: {e}")
        return None

def extract_markdown_section(markdown_text, heading):
    """提取指定 Markdown 二级标题整个 section。"""
    if not markdown_text or not heading:
        return None

    pattern = re.compile(
        rf"(^##\s+{re.escape(heading)}\s*$.*?)(?=^##\s+|^\*\*Full Changelog\*\*:|\Z)",
        re.MULTILINE | re.DOTALL)
    match = pattern.search(markdown_text)
    return match.group(1).strip() if match else None

def extract_full_changelog_line(markdown_text):
    """提取 Full Changelog 行。"""
    if not markdown_text:
        return None

    pattern = re.compile(r"^\*\*Full Changelog\*\*:\s+.+$", re.MULTILINE)
    match = pattern.search(markdown_text)
    return match.group(0).strip() if match else None

def strip_trailing_release_metadata(markdown_text):
    """移除 AI 已输出的 New Contributors / Full Changelog，避免重复追加。"""
    if not markdown_text:
        return ""

    cleaned = re.sub(
        r"\n*##\s+New Contributors\s*\n.*?(?=\n\*\*Full Changelog\*\*:|\Z)",
        "\n",
        markdown_text,
        flags=re.DOTALL)
    cleaned = re.sub(r"\n*\*\*Full Changelog\*\*:\s+.+$", "", cleaned, flags=re.MULTILINE)
    return cleaned.rstrip()

def build_full_changelog_line(repo, previous_tag, current_tag):
    """构造 Full Changelog 行。"""
    if repo and previous_tag and current_tag:
        return f"**Full Changelog**: https://github.com/{repo}/compare/{previous_tag}...{current_tag}"
    return None

def finalize_changelog(changelog, github_generated_notes, repo, previous_tag, current_tag):
    """统一在尾部追加 New Contributors 和 Full Changelog。"""
    cleaned = strip_trailing_release_metadata(changelog)
    trailing_sections = []

    new_contributors = extract_markdown_section(github_generated_notes, "New Contributors")
    if new_contributors:
        trailing_sections.append(new_contributors)

    full_changelog_line = extract_full_changelog_line(github_generated_notes)
    if not full_changelog_line:
        full_changelog_line = build_full_changelog_line(repo, previous_tag, current_tag)
    if full_changelog_line:
        trailing_sections.append(full_changelog_line)

    if not trailing_sections:
        return cleaned.rstrip() + "\n"

    parts = [cleaned.rstrip()] if cleaned.strip() else []
    parts.extend(trailing_sections)
    return "\n\n".join(parts).rstrip() + "\n"

def call_ai_api(api_url, api_key, model, commits, github_generated_notes=None, previous_tag=None, current_tag=None, language="zh-CN"):
    """调用 AI 生成日志"""
    import requests

    prompt = f"""我将提供 git commit 的详细信息，包括提交信息、修改的文件列表以及代码差异（Diff）。
请分析这些代码变更，理解其实际修改的逻辑，生成一份面向最终用户的 Release Note。

版本范围：{previous_tag or '起始提交'} -> {current_tag}

要求：
1. **深度分析**：不要只看 Commit Message（有时开发者写得很随意），要通过 Diff 代码变更推断实际功能变化。例如，如果修改了 `Resources.resw`，可能是"新增多语言支持"；如果修改了 `Package.appxmanifest` 的版本号，可以忽略。
2. **严格区分"新功能"与"重构/迁移"**：
   - **新功能 (feat)**：必须存在用户可感知的新行为、新入口、新交互。代码出现在新文件中并不代表是新功能——如果逻辑是从旧文件删除并移到新文件，这是重构/迁移，不是新功能。
   - **重构/迁移 (refactor)**：代码从一处移动到另一处（如从 ViewModel 下沉到 Service 层、提取 Helper 方法），或消除重复逻辑、改善代码结构。归入"🔨 内部变更"或"⚡ 优化"，绝不能归入"✨ 新增功能"。
   - **判断方法**：对比 Diff 中的增删内容，如果新文件中增加的代码与旧文件中删除的代码高度相似，这就是迁移。
3. **尊重 Commit Message 前缀**：`feat:` = 新功能，`fix:` = 修复，`refactor:` = 重构，`chore:` = 杂务，`docs:` = 文档，`perf:` = 性能优化。前缀是重要的分类参考依据，但仍需结合 Diff 验证。
4. **语言**：请使用简体中文。
5. **格式**：Markdown 格式。
6. **分类**（仅使用以下分类，没有内容的分类直接省略）：
   - "✨ 新增功能"：用户可感知的全新功能
   - "⚡ 优化"：对已有功能的体验改进、性能提升
   - "🛠️ 修复"：Bug 修复
   - "🔨 内部变更"：重构、迁移、代码结构调整、依赖更新等用户无感知的改动
7. **内容精简**：
   - 如果某个功能被多次 Commit 修改，请合并为一条。
   - 忽略纯粹的格式化、注释修改、CI/CD 配置文件调整（除非影响到发布产物）。
    - 每个条目末尾都必须包含贡献者与主要 Commit 的短 Hash，格式优先为 `(by @xxx) (3a401ae, a528963)`。
    - 如果同一条目涉及多位贡献者或多个主要 Commit，可以列出多个用户名和多个短 Hash，但保持上述格式。
    - 提交详情中会显式提供 `Canonical Contributor` 字段。只要它存在，就优先直接使用该值，不要自行改写。
    - 绝对不要根据 `Author Name` 推断、拼接或猜测 `@用户名`。只有 `Canonical Contributor` 或 GitHub 自动生成说明中明确给出的 `@username` 才能使用 `@`。
    - 如果 `Canonical Contributor` 不是 `@username` 形式，而是普通名字，则按原样输出贡献者名字，不要强行补 `@`。
8. **输出要求**：最终返回内容仅包含更新日志本身，不要有任何开场白、解释或总结性文字（如"好的"、"以下是更新日志"等）。
9. **尾部保留策略**：不要在正文里自行生成 `## New Contributors` 与 `**Full Changelog**`；它们会由后处理统一追加。
10. **示例**：
```markdown
## 更新日志

### ✨ 新增功能
*   **地图重命名与导出**：在地图管理页面新增了重命名和导出为 ZIP 文件的功能。(by @alice) (1c91359)

### 🛠️ 修复
*   **下载源未生效**：修复了用户设置 BMCLAPI 下载源后，实际下载仍走官方源的问题。(by @bob) (0e3c49f)

### 🔨 内部变更
*   **整合包安装逻辑重构**：将整合包安装逻辑从 ViewModel 下沉至 Core 服务层，改善代码结构。(by @charlie) (cc89355)
*   **代码清理**：移除启动视图模型中的废弃方法和冗余逻辑。(by @charlie) (e6283dc)
```

以下是 GitHub 自动生成的发布说明参考（优先用它补足贡献者、PR、新贡献者和 Full Changelog 上下文）：
{github_generated_notes or '（未获取到 GitHub 自动生成的发布说明）'}

以下是提交记录详情：
{commits}
"""

    system_prompt = """你是 XianYuLauncher 的专业发布日志生成助手。
直接输出 Markdown 更新日志正文，不要输出开场白、解释、总结或代码块围栏。
必须严格遵守：
- 使用简体中文。
- 仅使用以下分类标题：✨ 新增功能、⚡ 优化、🛠️ 修复、🔨 内部变更。
- 每个条目末尾都必须包含贡献者和短哈希；优先使用 `(by @xxx) (3a401ae, a528963)` 格式。
- `Canonical Contributor` 是贡献者字段的最高优先级，必须直接使用，不要自行改写。
- 绝对不要根据 `Author Name` 自行猜测 `@username`。
- `## New Contributors` 与 `**Full Changelog**` 不要由你输出，后处理会统一追加。
"""

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json"
    }
    
    data = {
        "model": model,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": prompt}
        ],
        "temperature": 0.5
    }
    
    print(f"Sending request to {api_url} with model {model}...")
    
    try:
        response = requests.post(f"{api_url}/chat/completions", headers=headers, json=data, timeout=60)
        
        if response.status_code != 200:
            print(f"API Error: {response.status_code} - {response.text}")
            return None
            
        result = response.json()
        content = result['choices'][0]['message']['content']
        return content
    except Exception as e:
        print(f"Exception calling AI: {e}")
        return None

def main():
    parser = argparse.ArgumentParser(description='Generate Changelog using AI')
    parser.add_argument('--token', required=False, help='AI API Key')
    parser.add_argument('--base-url', default='https://api.openai.com/v1', help='AI API Base URL')
    parser.add_argument('--model', default='gpt-3.5-turbo', help='Model name')
    parser.add_argument('--current-ref', required=True, help='Current Tag or SHA')
    parser.add_argument('--repo', default='', help='GitHub repo in owner/name format')
    parser.add_argument('--github-token', default='', help='GitHub token used for generate-notes API')
    parser.add_argument('--output', default='changelog.md', help='Output file')
    
    args = parser.parse_args()
    
    print(f"Current Ref: {args.current_ref}")
    
    # 1. 自动寻找上一个 Tag
    prev_tag = get_previous_tag(args.current_ref)
    print(f"Previous Tag identified: {prev_tag}")

    github_generated_notes = get_github_generated_release_notes(
        args.repo,
        args.github_token,
        args.current_ref,
        prev_tag)
    if github_generated_notes:
        print("GitHub generated release notes fetched successfully.")
    else:
        print("GitHub generated release notes unavailable.")
    
    # 2. 获取 Commits
    commits = get_git_commits(prev_tag, args.current_ref, args.repo, args.github_token)
    
    if not commits:
        print("No commits found.")
        # 如果没有提交，生成一个默认的
        with open(args.output, 'w', encoding='utf-8') as f:
            f.write("本次更新包含若干修复和改进。")
        return

    print(f"Found {len(commits.splitlines())} commits.")
    
    # 3. 调用 AI
    changelog = None
    if args.token:
        print("Generating changelog via AI...")
        changelog = call_ai_api(
            args.base_url,
            args.token,
            args.model,
            commits,
            github_generated_notes,
            prev_tag,
            args.current_ref)
    else:
        print("AI token missing. Skipping AI generation.")
    
    if not changelog:
        if github_generated_notes:
            print("Failed to generate changelog with AI. Fallback to GitHub generated release notes.")
            changelog = github_generated_notes
        else:
            print("Failed to generate changelog with AI. Fallback to raw commits.")
            changelog = "## Update Log\n\n" + "\n".join([f"- {line}" for line in commits.splitlines()])

    changelog = finalize_changelog(changelog, github_generated_notes, args.repo, prev_tag, args.current_ref)
    
    # 4. 输出
    print("Writing output...")
    with open(args.output, 'w', encoding='utf-8') as f:
        f.write(changelog)
        
    # 为了让 GitHub Actions step output 也能拿到，写入 GITHUB_OUTPUT (如果是单行或简单多行)
    # 复杂的多行文本建议完全依赖文件，或者使用 EOF 标记
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as f:
            f.write(f"changelog_file={args.output}\n")

if __name__ == "__main__":
    main()
