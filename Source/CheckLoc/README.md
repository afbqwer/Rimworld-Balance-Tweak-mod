# CheckLoc - 本地化辅助工具

BalanceTweak 模组的本地化翻译键检查工具，检测翻译键的**缺失**、**重复**和**多余**问题。

## 运行方式

在项目根目录执行：

```powershell
# 全部检查
dotnet run --project Source/CheckLoc

# 仅检测缺失翻译键
dotnet run --project Source/CheckLoc -- --check missing

# 仅检测重复翻译键
dotnet run --project Source/CheckLoc -- --check duplicate

# 仅检测多余翻译键
dotnet run --project Source/CheckLoc -- --check orphaned

# 仅检查指定语言
dotnet run --project Source/CheckLoc -- --lang ChineseSimplified
dotnet run --project Source/CheckLoc -- --lang English

# 查看帮助
dotnet run --project Source/CheckLoc -- --help

# 显示 Comment 键详情（默认仅显示摘要）
dotnet run --project Source/CheckLoc -- --show-comments

# --- 自动修复模式 ---

# 检测并修复重复翻译键
dotnet run --project Source/CheckLoc -- --fix duplicate

# 检测并修复多余翻译键
dotnet run --project Source/CheckLoc -- --fix orphaned

# 全部检测并修复
dotnet run --project Source/CheckLoc -- --fix all

# 指定语言修复
dotnet run --project Source/CheckLoc -- --fix duplicate --lang ChineseSimplified
```

## 命令行参数

| 参数 | 可选值 | 默认值 | 说明 |
|------|--------|--------|------|
| `--check` | `all`, `missing`, `duplicate`, `orphaned` | `all` | 检查类型 |
| `--fix` | `all`, `duplicate`, `orphaned` | — | 自动修复模式（先检测后修复） |
| `--lang` | `all`, `English`, `ChineseSimplified` | `all` | 目标语言 |
| `--show-comments` | — | 关闭 | 显示 Comment 后缀键详情（默认仅一行摘要） |

## 检测逻辑

### 缺失翻译键检测 (`--check missing`)

扫描 C# 源码中所有被引用的 `MST.xxx` 键，与 `Languages/{语言}/Keyed/*.xml` 中已定义的键对比，找出缺失项。

代码中的键来源于四种方式：

| 来源 | 示例 | 说明 |
|------|------|------|
| 硬编码字符串 | `"MST.Apply".Translate()` | 直接匹配正则 |
| Enum 动态生成 | `typeof(SettingType).GetEnumNames().Select(s => "MST." + s)` | 解析枚举定义，为每个成员名生成 `MST.{Name}` |
| Enum.GetNames | `Enum.GetNames(typeof(SettingType)).Select(s => "MST." + s)` | 同上，另一种调用形式 |
| TweakField 字段 | `("MST." + field.Name).Translate()` | 解析 `[TweakField]` 属性下方的字段名，生成 `MST.{Name}` 和 `MST.{Name}Comment` |

输出分为两级：

| 级别 | 说明 |
|------|------|
| `[WARN]` | 必须的翻译键缺失，应尽快补齐 |
| `[INFO]` | Comment 后缀键（可选 tooltip），带 `StatDef` 的字段可由游戏自带描述代替，可忽略 |

### 重复翻译键检测 (`--check duplicate`)

检测两类重复：

| 类型 | 说明 |
|------|------|
| 同文件内重复 | 同一 XML 文件内同一键名出现多次。
| 跨文件重复 | 同一键名在不同 XML 文件中定义。

### 多余翻译键检测 (`--check orphaned`)

与缺失检测相反：找出 XML 翻译文件中已定义但 C# 代码中从未引用的键。这些键可能是：

- 代码重构后遗留的旧翻译键
- 字段改名后残留的旧键名
- 手动添加但从未使用的键

> **注意**：多余键检测受代码扫描精度影响。如果无法识别所有动态键生成方式，可能出现少量误报。建议人工确认后清理。

## 自动修复功能 (`--fix`)

新增 `--fix` 参数可自动清除检测出的问题。修复前会先运行对应的检测并输出报告。

### 重复键修复 (`--fix duplicate`)

| 场景 | 策略 |
|------|------|
| 同文件内重复 | 保留第一次出现的行（最小编号行），删除后续重复行 |
| 跨文件重复（含 Core.xml） | 保留 Core.xml 中的行，删除其他文件中的行 |
| 跨文件重复（无 Core.xml） | 不自动修复，仅报告 |

### 多余键修复 (`--fix orphaned`)

移除 XML 翻译文件中已定义但 C# 代码中未引用的所有键。默认会一并清理 Comment 后缀键。

### 安全说明

- 修改直接写入 XML 源文件
- **建议修复前通过 git 提交或暂存当前更改**，以便通过 `git diff` 审查改动，必要时用 `git checkout` 回滚

## 输出格式

```
=== BalanceTweak 本地化检查报告 ===
语言: ChineseSimplified
项目根目录: D:\...\Balance Tweak

--- 缺失翻译键 ---
[WARN] ChineseSimplified 缺少以下翻译键 (11 个):
  MST.lifespanYears           <- TweakField 字段 (Data\RaceData.cs)
  ...

[INFO] ChineseSimplified 缺失 281 个 Comment 键（可选 tooltip）。使用 --show-comments 查看详情。

--- 重复翻译键 ---
[ERROR] ChineseSimplified/Core.xml: MST.Save (第54行, 第75行)
...

--- 多余翻译键 ---
[WARN] ChineseSimplified 有 93 个多余翻译键（XML 中定义但代码未引用）:
  MST.activityResearchFactorCurve          <- Race.xml
  ...

--- 重复键修复 ---
[FIX] ChineseSimplified/Core.xml: 移除同文件重复键 MST.Save (第54行, 第75行)
[FIX] ChineseSimplified/Race.xml: 移除跨文件重复键 MST.SomeKey (第30行)

--- 多余键修复 ---
[FIX] ChineseSimplified/Race.xml: 移除多余键 MST.oldKey1
[FIX] English/Ability.xml: 移除多余键 MST.deprecatedKey

=== 摘要 ===
检查完成，发现 282 个问题。
已自动修复 5 个问题。
```

## 返回值

- `0` — 未发现问题或所有问题均已修复
- `1` — 仍有未修复的问题（可用作 CI 失败判定）

## 项目结构

```
Source/CheckLoc/
├── CheckLoc.csproj          # 控制台项目（net10.0，无 RimWorld 依赖）
├── Program.cs               # 入口 + 参数解析 + ANSI 彩色输出
├── Models.cs                # XmlKeyInfo / CodeKeyRef 数据模型
├── XmlKeyExtractor.cs       # 从 XML 提取翻译键 + 同文件内重复检测
├── CodeKeyExtractor.cs      # 从 C# 源码提取引用的翻译键（四种来源）
├── EnumResolver.cs          # 解析 C# 枚举定义
├── MissingKeyDetector.cs    # 缺失翻译键检测
├── DuplicateDetector.cs     # 重复翻译键检测
├── DuplicateFixer.cs        # 重复翻译键自动修复
├── OrphanedKeyDetector.cs   # 多余翻译键检测
└── OrphanedKeyFixer.cs      # 多余翻译键自动修复
```

## 已知限制

- **纯正则解析**：不做完整 C# 编译，注释中的 `MST.xxx` 字符串可能被当作引用
- **多余键误报**：部分键可能通过反射或运行时动态拼接引用，静态正则扫描无法识别此类引用