using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// BodyDef 部位树的编辑用模型（仅核心字段）。
/// 行级/树级的字符串编解码在 <see cref="SerializationHelper.SerializeBodyPartTree"/> 中；
/// 与 <see cref="BodyPartRecord"/> 的双向转换及写回 def 由 <see cref="BodyPartTreeCodec"/> 负责。
/// </summary>
public class BodyPartNode
{
    public BodyPartDef? def;
    public string? customLabel;
    public float coverage = 1f;
    public BodyPartHeight height = BodyPartHeight.Undefined;
    public BodyPartDepth depth = BodyPartDepth.Undefined;
    public List<BodyPartGroupDef> groups = new();
    public List<BodyPartNode> children = new();

    /// <summary>仅运行时用：树窗口的展开/折叠状态，不参与编码。</summary>
    public bool expanded = true;
}

/// <summary>
/// <see cref="BodyPartRecord"/> / <see cref="BodyDef.corePart"/> 与 <see cref="BodyPartNode"/> 之间的转换与应用。
/// 具体的字符串编码（<c>List&lt;string&gt;</c>）见 <see cref="SerializationHelper"/> 的 “BodyPart tree” 区。
/// </summary>
public static class BodyPartTreeCodec
{
    private static readonly FieldInfo? CachedAllPartsField = AccessTools.Field(typeof(BodyDef), "cachedAllParts");

    #region Record <-> Node

    public static BodyPartNode? FromRecord(BodyPartRecord? rec)
    {
        if (rec == null) return null;
        var node = new BodyPartNode
        {
            def = rec.def,
            customLabel = rec.customLabel,
            coverage = rec.coverage,
            height = rec.height,
            depth = rec.depth,
            groups = rec.groups != null ? new List<BodyPartGroupDef>(rec.groups) : new List<BodyPartGroupDef>(),
        };
        if (rec.parts != null)
        {
            foreach (var child in rec.parts)
            {
                if (child == null) continue;
                var c = FromRecord(child);
                if (c != null) node.children.Add(c);
            }
        }
        return node;
    }

    public static BodyPartRecord ToRecord(BodyPartNode node)
    {
        var rec = new BodyPartRecord
        {
            def = node.def,
            customLabel = node.customLabel,
            coverage = node.coverage,
            height = node.height,
            depth = node.depth,
            groups = node.groups != null ? new List<BodyPartGroupDef>(node.groups) : new List<BodyPartGroupDef>(),
            parts = new List<BodyPartRecord>(),
        };
        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                if (child == null) continue;
                rec.parts.Add(ToRecord(child));
            }
        }
        return rec;
    }

    #endregion

    #region Encode / Decode

    public static List<string>? Encode(BodyPartRecord? root) => SerializationHelper.SerializeBodyPartTree(FromRecord(root));

    public static List<string>? Encode(BodyPartNode? root) => SerializationHelper.SerializeBodyPartTree(root);

    public static BodyPartNode? Decode(List<string>? lines) => SerializationHelper.DeserializeBodyPartTree(lines);

    #endregion

    #region Apply

    /// <summary>
    /// 把编码后的部位树写回 <paramref name="def"/> 并重建其缓存。
    /// 编码为空 / 根节点解析失败时不做改动。
    /// </summary>
    public static void SetCorePart(BodyDef def, List<string> encoded)
    {
        var root = Decode(encoded);
        if (root == null) return;
        def.corePart = ToRecord(root);
        RebuildCaches(def);
    }

    /// <summary>
    /// 重建 BodyDef 的运行期缓存。
    /// <c>cachedAllParts</c> 是 <c>[Unsaved]</c> 私有列表，且 <c>CacheDataRecursive</c> 只会往它里面**追加**，
    /// 所以必须先清空再 <c>ResolveReferences()</c>，否则每次 Apply 都会让 AllParts 翻倍。
    /// </summary>
    private static void RebuildCaches(BodyDef def)
    {
        if (CachedAllPartsField?.GetValue(def) is List<BodyPartRecord> cached)
            cached.Clear();
        def.ClearCachedData();
        def.ResolveReferences();
    }

    #endregion
}