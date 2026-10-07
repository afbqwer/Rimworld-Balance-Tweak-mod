namespace CheckLoc;

/// <summary>
/// 在 XML 翻译文件中找到的键。
/// </summary>
public sealed record XmlKeyInfo(string Key, string FileName, string Language, int LineNumber);

/// <summary>
/// 在 C# 源码中引用的翻译键，附带来源信息。
/// </summary>
public sealed record CodeKeyRef(string Key, string Source, string OriginFile);