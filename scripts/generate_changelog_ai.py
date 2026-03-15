import os
import sys
import subprocess
import json
import requests
import argparse

def get_git_commits(last_tag, current_ref):
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
        MAX_DIFF_LEN = 1500  # 每个 Commit 最多读取多少字符的 Diff，防止 Token 爆炸
        
        print(f"Analyzing {len(hashes)} commits for diffs...")
        
        # 倒序处理（从旧到新），或者正序都可以，git log 默认是新到旧
        for commit_hash in hashes:
            if not commit_hash: continue
            
            # 获取 Author 和 Subject
            info_cmd = ["git", "show", "-s", "--format=Commit: %h%nAuthor: %an%nDate: %cd%nMessage: %s%n%b", commit_hash]
            info = subprocess.run(info_cmd, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout.strip()
            
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

def call_ai_api(api_url, api_key, model, commits, language="zh-CN"):
    """调用 AI 生成日志"""
    
    prompt = f"""你是一个负责生成软件发布更新日志的智能助手。
我将提供 git commit 的详细信息，包括提交信息、修改的文件列表以及代码差异（Diff）。
请分析这些代码变更，理解其实际修改的逻辑，生成一份面向最终用户的 Release Note。

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
   - 每个条目末尾加上主要 Commit 的短 Hash（如 `(b9c2a1)`）。
8. **输出要求**：最终返回内容仅包含更新日志本身，不要有任何开场白、解释或总结性文字（如"好的"、"以下是更新日志"等）。
9. **示例**：
```markdown
## 更新日志

### ✨ 新增功能
*   **地图重命名与导出**：在地图管理页面新增了重命名和导出为 ZIP 文件的功能。(1c91359)

### 🛠️ 修复
*   **下载源未生效**：修复了用户设置 BMCLAPI 下载源后，实际下载仍走官方源的问题。(0e3c49f)

### 🔨 内部变更
*   **整合包安装逻辑重构**：将整合包安装逻辑从 ViewModel 下沉至 Core 服务层，改善代码结构。(cc89355)
*   **代码清理**：移除启动视图模型中的废弃方法和冗余逻辑。(e6283dc)
```

以下是提交记录详情：
{commits}
"""

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json"
    }
    
    data = {
        "model": model,
        "messages": [
            {"role": "system", "content": "你是一个专业的发布日志生成助手。直接输出更新日志内容，不要有任何开场白或解释。"},
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
    parser.add_argument('--token', required=True, help='AI API Key')
    parser.add_argument('--base-url', default='https://api.openai.com/v1', help='AI API Base URL')
    parser.add_argument('--model', default='gpt-3.5-turbo', help='Model name')
    parser.add_argument('--current-ref', required=True, help='Current Tag or SHA')
    parser.add_argument('--output', default='changelog.md', help='Output file')
    
    args = parser.parse_args()
    
    print(f"Current Ref: {args.current_ref}")
    
    # 1. 自动寻找上一个 Tag
    prev_tag = get_previous_tag(args.current_ref)
    print(f"Previous Tag identified: {prev_tag}")
    
    # 2. 获取 Commits
    commits = get_git_commits(prev_tag, args.current_ref)
    
    if not commits:
        print("No commits found.")
        # 如果没有提交，生成一个默认的
        with open(args.output, 'w', encoding='utf-8') as f:
            f.write("本次更新包含若干修复和改进。")
        return

    print(f"Found {len(commits.splitlines())} commits.")
    
    # 3. 调用 AI
    print("Generating changelog via AI...")
    changelog = call_ai_api(args.base_url, args.token, args.model, commits)
    
    if not changelog:
        print("Failed to generate changelog with AI. Fallback to raw commits.")
        changelog = "## Update Log\n\n" + "\n".join([f"- {line}" for line in commits.splitlines()])
    
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
