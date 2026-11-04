using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace BalanceTweak;

/// <summary>
/// 从 <see cref="QuestScriptDef.questNameRules"/> 里拼一个可读的任务名，<b>只给列表标题用</b>。
///
/// <para>
/// <b>为什么不用原版的语法引擎</b>：<c>GrammarResolver.Resolve</c> 要有 slate / pawn / 随机数才跑得起来，
/// 这里只是想在表格里显示一行字 —— 用它会掷骰子（同一行每次刷新名字都不一样）、
/// 会用 <c>Find.CurrentMap</c> 之类只有游戏内才有的状态，还可能把规则压进 QuestGen 的解析栈。
/// 所以这里只做最保守的<b>纯文本展开</b>：取 <c>questName</c> 规则，把 <c>[token]</c> 用同批规则里的
/// 第一个纯文本选项递归替换；<b>任何一步拿不到答案就换下一条候选，全都不行就返回 null</b>
/// （上层回落到 Def 自己的 label）。不掷骰子、不改任何全局状态、不抛异常。
/// </para>
///
/// <para>
/// <b>只认 <see cref="Rule_String"/></b>：<c>Rule_Number</c> / <c>Rule_NamePerson</c> 的
/// <c>Generate()</c> 会当场取随机数或找 pawn，正是要避免的那类副作用。代价是含
/// <c>[asker_nameDef]</c>、<c>[item]</c>（由任务生成时塞进 slate 的变量）这类 token 的名字拼不出来，
/// 于是原样放弃 —— 原版 74 个带名字规则的任务里大约六成能拼出来，其余回落 defName。
/// </para>
/// </summary>
internal static class QuestNameResolver
{
    private const string NameKeyword = "questName";

    /// <summary>token 最多递归几层（原版一般两层：<c>questName → [camp]</c> 就到头了）。</summary>
    private const int MaxDepth = 3;

    /// <summary>只试前几条候选：规则是按"越靠前越常用"排的，后面的多是特例。</summary>
    private const int MaxCandidates = 8;

    /// <summary>过长的名字多半是规则拼错了（带整段描述），不如不要。</summary>
    private const int MaxLength = 80;

    private static readonly Regex TokenRegex = new(@"\[([^\[\]]+)\]", RegexOptions.Compiled);

    /// <summary>defName(+语言) → 结果；空串代表"确认拼不出来"，避免每次显示都重算一遍。</summary>
    private static readonly Dictionary<string, string> Cache = new();

    public static string? TryResolve(QuestScriptDef? def)
    {
        if (def == null) return null;

        // 语言带上：换语言要重启，但把 key 写全总比事后怀疑缓存干净。
        var key = def.defName + "|" + (LanguageDatabase.activeLanguage?.folderName ?? string.Empty);
        if (Cache.TryGetValue(key, out var cached)) return cached.Length == 0 ? null : cached;

        string? result;
        try
        {
            result = Resolve(def);
        }
        catch (Exception e)
        {
            // 拼名字是锦上添花：解析失败不能影响表格，也不能刷 Warning（几十个 Mod 的规则都可能有毛病）。
            TweakLog.Message($"[BalanceTweak] 解析 {def.defName} 的 questNameRules 失败，改用 Def.label：{e.Message}");
            result = null;
        }

        Cache[key] = result ?? string.Empty;
        return result;
    }

    private static string? Resolve(QuestScriptDef def)
    {
        var keywords = new Dictionary<string, List<string>>();
        var candidates = new List<string>();

        // questDescriptionAndNameRules 会同时进名字与描述两个池（见 QuestScriptDef.InitializeRules），
        // 所以这里的 questName 规则同样算数。
        Collect(def.questNameRules, keywords, candidates);
        Collect(def.questDescriptionAndNameRules, keywords, candidates);

        var tried = 0;
        foreach (var candidate in candidates)
        {
            if (tried++ >= MaxCandidates) break;

            var name = Expand(candidate, keywords, 0);
            if (name == null) continue;

            name = Normalize(name);
            if (name.Length == 0 || name.Length > MaxLength) continue;
            if (name == def.defName) continue;   // 拼出来还是类名，等于没拼
            return name;
        }
        return null;
    }

    /// <summary>收集一个规则包里的纯文本规则：<c>questName</c> 进候选，其余进 token 词典。</summary>
    private static void Collect(RulePack? pack, Dictionary<string, List<string>> keywords, List<string> candidates)
    {
        if (pack == null) return;

        List<Rule>? rules;
        try
        {
            // .Rules 会把 include 的 RulePackDef 一并展开，且规则串在加载期已翻译好 —— 正是我们要的原文。
            // 它只是让这个 RulePack 自己把规则解析并缓存一次（幂等，游戏生成任务时本来也会走到），
            // 不碰 QuestGen 的解析栈 / slate。
            rules = pack.Rules;
        }
        catch (Exception e)
        {
            TweakLog.Message($"[BalanceTweak] 读取任务名规则失败：{e.Message}");
            return;
        }
        if (rules == null) return;

        foreach (var rule in rules)
        {
            if (rule is not Rule_String text) continue;   // 只认纯文本，见类注释
            var output = text.Generate();
            if (output.NullOrEmpty()) continue;

            if (rule.keyword == NameKeyword)
            {
                candidates.Add(output);
                continue;
            }
            if (rule.keyword.NullOrEmpty()) continue;

            if (!keywords.TryGetValue(rule.keyword, out var list))
                keywords[rule.keyword] = list = new List<string>();
            list.Add(output);
        }
    }

    /// <summary>
    /// 把 <c>[token]</c> 递归替换成选项里的第一条。拿不到选项就整条放弃 ——
    /// 宁可退回 defName，也不要显示半截 "<c>[asker_nameDef] 的救援</c>"。
    /// </summary>
    internal static string? Expand(string pattern, Dictionary<string, List<string>> keywords, int depth = 0)
    {
        if (depth > MaxDepth) return null;

        // 同一层里重复出现的 token 用同一个选项，读起来才像一句人话（"Raid on the Raid" 也好过两个随机词）。
        var sb = new StringBuilder(pattern.Length);
        var pos = 0;
        foreach (Match m in TokenRegex.Matches(pattern))
        {
            sb.Append(pattern, pos, m.Index - pos);
            pos = m.Index + m.Length;

            var key = m.Groups[1].Value.Trim();
            if (!keywords.TryGetValue(key, out var options) || options.Count == 0) return null;

            var expanded = Expand(options[0], keywords, depth + 1);
            if (expanded == null) return null;
            sb.Append(expanded);
        }
        sb.Append(pattern, pos, pattern.Length - pos);
        return sb.ToString();
    }

    /// <summary>折叠空白（规则里常有换行 / 连续空格）并去掉首尾空白。</summary>
    internal static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        var lastSpace = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace && sb.Length > 0) sb.Append(' ');
                lastSpace = true;
            }
            else
            {
                sb.Append(c);
                lastSpace = false;
            }
        }
        return sb.ToString().Trim();
    }
}
