![XianYu Launcher Contributing](/XianYuLauncher/Assets/ContributingHero_en.png)

[English](CONTRIBUTING.md) | [简体中文](docs/CONTRIBUTING_zh-CN.md) | [繁體中文](docs/CONTRIBUTING_zh-TW.md)

# Contributing

Thank you for your interest in XianYu Launcher. This document explains how to file issues and contribute code: the first half covers issues, the second covers pull requests and local build notes.

---

## Filing issues

### Where to start

1. Open the repository on GitHub → **Issues** → **New issue**.
2. **Prefer a template** (easier for maintainers to triage than a blank issue):
   - **Bug report**: crashes, incorrect behavior, anything that does not match expected behavior.
   - **Feature request**: new capabilities, UX improvements, product-oriented ideas.

Pick the matching type on the web form; the title prefix and label hints are applied for you.

### What to include for bugs

- **Launcher version**, **Windows version**, and relevant **ModLoader type/version** (if tied to a game instance).
- **Reproduction steps**: numbered steps (what you clicked, in what order). Steps that reliably reproduce the problem are the most valuable.
- **Logs**: the template explains how to open the log folder via **Settings → About → Quick actions → Open log folder**. Paste **relevant excerpts** in a fenced code block or attach a file—this helps far more than only writing “it crashed”.
- **Screenshots**: include them when there is a dialog or visible UI glitch.

If you are unsure whether it is a bug or a suggestion, choose the closer template; maintainers can re-label.

### What to include for feature requests

- **The pain point**: what is awkward today or costs extra time.
- **Expected behavior or UI**: be as specific as you can; put references (other apps, sketches) under “Additional context” if you have them.

### Other habits

- **One topic per issue** so threads stay trackable and closable.
- **Search existing issues** before opening a duplicate.
- **Any language is fine**; clear facts matter more than perfect wording.

---

## Code contributions (pull requests)

When changing the codebase, follow **`.github/copilot-instructions.md`** for architecture rules (downloads, dialogs, etc.)—the same guidance maintainers and Copilot use.

### Commit messages

We use **Conventional Commits**:

- **`type` in English**: e.g. `feat`, `fix`, `refactor`, `chore`.
- **`scope` optional**, e.g. `(protocol)`, `(ModDownloadDetailViewModel)`.
- **Subject and body**: use whatever language you usually write in; state **what changed and why**. For larger changes, add background and migration notes in the body.

Example:

```text
fix(settings): Fix settings item not taking effect

- Use correct keys for persisted settings reads
- Align all call sites
```

### Pull request workflow

1. Create a feature branch from `main`.
2. Keep the change **focused**: one PR should address one coherent concern; avoid unrelated formatting-only or huge drive-by refactors.
3. For bug fixes, **link the issue** in the PR description when possible and describe **how you verified** the fix.
4. Watch for review feedback; when there is major design disagreement, **open an issue first** to align before large code changes.

---

## Environment and build

Skip this section if you already have a WinUI / desktop development setup; use it to match what the repo expects.

- **OS**: Windows 10 1809 (17763) or later (same as README).
- **Tooling**: **.NET 10.0**; for WinUI work, use **Visual Studio 2026 or newer** with workloads such as **.NET desktop development**, **WinUI app development**, and **MSBuild**, as needed to open the solution.

**Common commands** (repository root):

```bash
# Main WinUI app
msbuild XianYuLauncher/XianYuLauncher.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal

# Core
dotnet build XianYuLauncher.Core/XianYuLauncher.Core.csproj

# Tests (replace <test-project.csproj> with the project you want to run)
dotnet test <test-project.csproj>
```

---

## More links

- **Documentation site** and **community (QQ / Bilibili, etc.)**: see README.

## Closing

- Thank you for contributing!
