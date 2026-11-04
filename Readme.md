# BalanceTweak 开发指南

改代码的操作指南：能做什么、在哪改、改完怎么验。
设计取舍、历史讨论与改进计划见 `Docs/`，本文不重复。

## 0. 硬约束（改代码前必读）

存档里每条修改的身份是三元组 `(数据类 FullName, defName, SettingType 枚举名)`，任一变动都会让旧数据失配。

| 禁止 | 说明 |
|------|------|
| 重命名 / 改 namespace / 删除仍在用的 `XxxData` 类 | 存档每条都写 `Class="BalanceTweak.XxxData"`，解析失败整条变 null（`Init` 有 null 防御，但数据会丢） |
| 重命名 / 删除 `SettingType` 枚举成员 | 存档按**名称**写入。重排声明顺序安全（须保留各成员原有显式数值），改数值才危险 |
| 新增 `SettingType` 成员却忘了写进 `TabOrder` | 显示顺序只认 `SettingType.cs` 的 `TabOrder`（见 §5.3）。漏了不会报错，页签直接从下拉里消失 —— `SettingTypeOrderTests` 会在 `dotnet test` 时报红 |
| 重命名 `[TweakField]` 字段 | 字段名 = XML 元素名，改名即丢该字段的修改（新增字段安全） |
| 直接删除仍在使用的 `[TweakFor]` 规则 | 先确认没有已存数据落在该页签；确需删除时保留一个继承它的空壳子类 |
| 用「值 == null / 值为空」反推"用户清空了该字段" | 三态只认两个**显式**集合（`TweakData.StateOf`）：`_modifiedFieldNames` / `_clearedFieldNames`。清空**只能**由 `MarkFieldCleared` 写入；判定一律走 `Apply*` 门控 |
| 把「清空」实现成给集合赋 `null` | 清空语义只有「从集合里移除项」，终态是**空集合**（`Count == 0`），永远不是 `null`。列表为 `null` 在原版里是错误状态，裸 `.Contains` / `foreach` 会 NRE。stat 列的清空 = `RemoveWhere` 掉那条 `StatModifier` |
| 绕过 `MarkFieldCleared` 改 `_clearedFieldNames`，或让同一字段同时进两个集合 | 两集合互斥是 Set/Cleared 的唯一判据，破坏后 `StateOf` 二义、`Apply` 门控错乱 |
| 给 `List<>` / `ThingFilter` 字段加 `??=` 回填却不带 `!IsFieldModified(...)` 守卫 | Cleared 字段读档后会被 Def 回填"复活"（SC2 报 `[BT/SELFCHECK] 复活:`） |
| 静态引用可选 Mod 的类型（`typeof` / `is` / 泛型参数 / 把该类型变量捕获进 lambda） | 未装该 Mod 的玩家启动期 `GetFields()` → `TypeLoadException`。一律按全名反射，闭包里只许出现 `object` / `MemberInfo` |
| 给可选 Mod 的 dll 加 csproj `<Reference>` | 有引用就能编出跨程序集 typeref；不引用 = 编译期杜绝。`PipeSystem` / `VEF` / `VanillaPsycastsExpanded` 均已移除引用 |

安全的改动：末尾追加枚举成员、新增 `[TweakField]` 字段、调整 `MatchMethod` 与 `Priority`、修改 `Apply` 逻辑。

## 1. Apply 门控（`TweakData.Typed.cs`，`protected internal`）

三态写入的**唯一入口**：按 `StateOf(f)` 决定写不写、怎么写，绝不用 `!= null` / `HasValue` 短路。
`Set` → 写当前值；`Unset` → 什么都不做；`Cleared` → 见下表最后一列。

| helper | 目标 | 签名 | `Cleared` 的动作 |
| ------ | ---- | ---- | ---------------- |
| `ApplyV<TV>` | 非空目标 / 标量（`Action<float>`、`Action<int>`、`Action<FloatRange>`…） | `ApplyV<TV>(string f, Action<TV> set)` | 还原原值（`Revert`） |
| `ApplyN<TV>` `where TV:struct` | 可空目标（`float?` / `int?` / `IntRange?` / `QualityCategory?`…） | `ApplyN<TV>(string f, Action<TV?> set)` | 还原原值——**不写 `null`**（置 `null` 等于重置，会与"本来没配"混成一态） |
| `ApplyL<TV>` | 集合（`List<T>`） | `ApplyL<TV>(string f, Action<List<TV>?> set)` | **写成空列表**，永不写 `null` |
| `ApplyD<TV>` `where TV:Def` | Def 引用 | `ApplyD<TV>(string f, Action<TV?> set)` | 还原原引用 |
| `ApplyS` | `string` | `ApplyS(string f, Action<string> set)` | 还原原字符串 |
| `ApplyF` | `ThingFilter` | `ApplyF(string f, Action<ThingFilter> set)` | 无 Cleared 分支（空过滤器是有效值） |

- `f` 一律写 `nameof(字段名)`：`ApplyV<float>(nameof(workAmount), v => def.workAmount = v)`。
- 标量选 `ApplyV` 还是 `ApplyN` 只看**目标字段在 Def 上是否可空**；两者 Cleared 行为相同。
- setter 收到的集合永远可安全遍历（`ApplyL` 不给 `null`），`v => def.list = v` 即够。
- `Revert` 原值取自 `TweakDatabase.GetOriginal(id)`；原值缺席时保持现状 + 每字段告警一次。
- **重置路径不看三态**：整行重置（`forceWriteAll`）、单格重置（`forceWriteFields`）由 `Apply*` 内部的 `IsForceWriteField(f)` 接管，直接把字段值当权威值写回。自定义判据（如"这次 Apply 有没有动过射速"）必须用 `IsForceWriteField` 而不是 `IsFieldModified`——重置是先写回原值再清标记，那一刻三态已是 `Unset`。
- `StatDef` 字段不用这些 helper，改用 `ApplyDefStats(def)`（见 §3.2 / §4.1）；字符串列不开放清空，`ApplyS` 的 Cleared 分支平时不走。

## 2. 可选 Mod 反射（`DataUtility`）

未加载可选 Mod 时不得出现跨程序集 typeref：跨 Mod 兼容一律按**字符串全名**访问，命名空间 `using` 与 csproj `<Reference>` 都不要加。

| helper | 用途 |
| ------ | ---- |
| `IsTypeOrSubclassOfName(Type?, string)` | 类型或其基类是否等于某全名（替代 `is` / `typeof(...) ==`） |
| `FindMember(Type?, string)` | 沿继承链找 `MemberInfo`（字段 / 属性，含非公开） |
| `GetMember` / `SetMember(object?, MemberInfo?, ...)` | 用已缓存的 `MemberInfo` 读写 |
| `GetByName(...)` / `SetByName(...)` | 一步式（内部 `FindMember`）；高频访问请先缓存 `MemberInfo` |
| `AsFloat` / `AsInt` / `AsBool` / `AsIntList(object?)` | 反射结果安全转换（失败返回 `null` / `false`） |
| `FindModExtension(Def?, string)` | 按全名找 `DefModExtension` |
| `MayRequire(string?)` | 判断某 Mod 是否加载（`[TweakFor(MayRequire = ...)]` 也用它） |

**lambda 只许捕获 `object` / `MemberInfo`**。参考实现：`Data/AbilityData.cs`、`Data/ProcessItems/RecipeProcessHelper.cs`。

原版 / 本工程类型的私有成员不用这套：一律 `Reflect.Field(type, "字段名")`（`AccessTools.Field` + 缺失即登记，把"1.7 改名导致的静默 null"变成启动日志里的一条）。

> **回归防线**：`Tests/AssemblyReferenceTests` 读产物元数据表，断言 `AssemblyRef` / `TypeRef` 里没有任何可选 Mod 程序集。`typeof` / `is` / 泛型参数 / 字段类型 / lambda 捕获（编译器会给闭包类生成公共字段）都会留下 typeref，加了就立刻跑红——不用等人肉 review 才发现。字符串写法则不产生 typeref，所以反射写法是安全的。

### 2.1 失配必须留痕（`Diagnostics`）

光"按名反射"不够：拿不到成员就返回 `null`，调用点一句 `as bool?` 之后还是 `null`，那一列从此恒空白 —— 玩家以为"这个 Mod 不支持"，日志里一个字都没有。所有失败都进 `Diagnostics` 登记表，初始化收尾由 `Diagnostics.Report()` 合成**一条** `Log.Error`（挂在 `finally` 里，`Init` 抛异常也照发）。

| 失败形态 | 登记点 | 日志样例 |
|---|---|---|
| 成员找不到 | `DataUtility.FindMember`（`GetByName` / `SetByName` / `FindMember` 自动，`[CallerMemberName]` 带出调用方法） | `成员 Fortified.CompProperties_SubRelay.relayRange —— 来自 LoadRelay` |
| 跨 Mod 类型不存在 | `TweakRegistry.TryCreateRule`（`[TweakFor(TypeName=...)]`） | `类型 X —— 来自 AbilityData 的 [TweakFor]（页签 Ability）；所需的 Mod Y 已启用，但该类型不存在` |
| 可选 Mod 类型不存在 | `DataUtility.OptionalType` | 同上（未给 packageId 时按"归属程序集已加载"判定） |
| 原版私有成员找不到 | `Reflect.Field` | `字段 ResearchProjectDef.cachedDescription —— 来自 Reflect.Field` |

四条口径：

- **「目标为 null」不登记**。`GetByName(null, "x")` 是上游没拿到对象（该 Def 没有这个扩展 / Mod 未装），不是成员缺失。只有"类型在、成员不在"才算失配。
- **同一 (类型, 成员) 只登记一条**，只累加次数。一次版本升级改名五个字段时日志是五行，不是五百行；绘制期反复走到失配也只有两条字典查找。
- **可选 Mod 类型一律走 `OptionalType`，禁止裸 `AccessTools.TypeByName`**。后者让「没装」和「装了但改名」长得一模一样，而两者处理方式相反：前者静默跳过（设计行为），后者必须报。判据就是 `MayRequire`；没有 packageId 时退回程序集短名比对。
- **失败走 `Log.Error`（永远输出），成功清单走 `TweakLog.Message`**（受「冗杂日志」开关控制）—— 没装 Mod 导致规则跳过不是错误，别用 Error 污染玩家日志。

### 2.2 排查「改了没反应 / 某列全空」

按顺序看启动日志，通常三步内定位：

1. `[BalanceTweak] 可选 Mod 兼容`（需开冗杂日志）—— 目标 Mod 是「已启用」还是「未启用（相关规则跳过）」。
2. `[BalanceTweak] ===== 兼容性诊断：反射失配` —— 有它就到此为止：清单直接给出 `类型.成员` + 谁在问。
3. `TweakRegistry 注册失败` / `有 N 条已存数据含无法解析的引用`（Def 被删）/ `N 条已存数据的类型无法识别`（Data 类改名）—— 分别对应注册期与存档侧失配。

三步都干净，说明反射层是好的，问题在 `Apply` 门控或 Def 解析；再查 §1 的 `StateOf` 与 `failedResolveItems`。

## 3. 新增可编辑 Def 类型

### 3.1 查 RimWorld 源码

按需用 `decompiler` / `rimworld-source`（C# 定义）与 `rimworld-data`（游戏内实际数据）工具。

### 3.2 参考已有 Data 文件

在 `Source/BalanceTweak/Data/` 找类似模板（如 `WeatherData`、`FoodData`）。关键模式：

- 类继承 `TweakData<T>`，`[TweakField()]` 标记字段，实现 `SetDef/Apply/GetPropType/TypeStrings`，加 `[TweakFor]`。
- **Stat 写入**：`Apply()` 里调用基类 `ApplyDefStats(def)` 一行即可 —— 它按 `IsEquippedStat` / `IsStuffFactorStat` / `IsStuffOffsetStat` 选择写入位置，并把 `statBases` 等 `List<StatModifier>` 字段与 def 双向同步（`StatModList` 样式字段一并处理，不需要额外的 Sync 方法）。
- 自带子项（阶段 / 招式 / 程度…）时实现 `ISubItemHost`（§3.6）。
- 分类不需改 `TweakDatabase`：分类、Apply 阶段的 Def 解析、`GetData(Def)` 探测全由 `[TweakFor]` 驱动。

### 3.3 编写 `{Name}Data.cs`

| 字段来源 | 读写方式 | 示例 |
|---------|---------|------|
| **StatDef 字段** | `[TweakField(StatDef = "字段名")]`，由 `ApplyDefStats` 写入 | Nutrition |
| **直接字段** | 直接读写 `def.字段名` | `stackLimit` |
| **子对象字段** | `def.ingestible.字段名`，SetDef/Apply 中判空 | `baseIngestTicks` |
| **Enum 字段** | `[TweakField(Style = ColumnStyle.Enum, EnumType = typeof(XxxEnum))]` | FoodPreferability |
| **Def 引用字段** | `[TweakField(Style = ColumnStyle.DefSelector)]` | `foragedFood`、`reloadableAmmoDef` |
| **`List<T>`** | `[TweakField(Style = ColumnStyle.DefList)]`，回填带守卫 | `makeImmuneTo`、`enablesNeeds` |

默认值来源按字段类型分三种，**不要一律写 `??=`**：

- **`[TweakField(StatDef = "...")]` 字段**：**不写** `??=`，由 `StatFieldMeta` 的 `DefaultGetter` + `InitFieldValue` 从 def 回填，已改 / 已清空的字段不会被覆盖。
- **标量 / `string` / Def 引用 / 子对象字段**：`field ??= def.字段名;`
- **`List<T>` / `ThingFilter`**：必须带守卫（硬约束表）：
  ```csharp
  if (!IsFieldModified(nameof(makeImmuneTo))) makeImmuneTo ??= stage.makeImmuneTo;
  ```
  先例：`HediffStageData.cs:243`、`FoodData.cs:129`、`TraderData.cs:87`。

> **private 字段**：目标字段非公开（如 `BiomeDef.wildAnimals`）时，用 `Reflect.Field(type, "字段名")` 缓存 `FieldInfo` 再读写 —— 它内部是 `AccessTools.Field` + 找不到就 `Log.Error`，别直接用裸 `AccessTools`（1.7 改名时只会得到静默 null，最后在某处 NRE）。先例 `BiomeData.cs`。
> **懒加载缓存**：Def 把派生数据缓存在私有字段里（`BiomeDef.cached*` 等，无失效机制）时，Apply 写完列表要反射把这些缓存置 `null` 强制重建，否则运行中不生效（参考 `BiomeData.cs`）。
> **复合对象子字段的列表**：按子字段逐个建 `[TweakField]`，Apply 中任一子字段非空时先 `def.fishTypes ??= new BiomeFishTypes()` 再赋值；DLC 内容用 `[TweakField(MayRequire = "ludeon.rimworld.odyssey")]` 门控列显示。

### 3.4 处理 Comp（CompProperties）

- **Bool 开关（添加/移除 Comp）**：参考 `ApparelData.isCompQuality`，`true` 添加、`false` 移除（先检查防重复）。
- **修改已有 Comp 属性**：参考 `BuildingData`，遍历 `def.comps` 用 `is` 匹配子类型后读写字段。
- **存在性检查**：`def.HasComp<T>()` / `HasAssignableCompFrom<T>()` / `GetCompProperties<T>()` / `comps.Any(...)`。
- **添加**：`new CompProperties(typeof(CompX))`，添加前务必查重。

### 3.5 声明分类规则（唯一注册步骤）

在 Data 类上加 `[TweakFor]`，声明负责哪一类 Def、显示在哪个页签、分类优先级：

```csharp
[TweakFor(typeof(WeatherDef), SettingType.Weather)]
class WeatherData : TweakData<WeatherData> { /* ... */ }
```

| 方式 | 写法 |
|------|------|
| 专用 Def 类型 | 构造参数直接写 Def 类型（`DefType` 是只读属性，只能由构造参数给） |
| ThingDef 子类别 | 构造参数写 `typeof(ThingDef)` + `MatchMethod = nameof(DataUtility.MatchWeapon)`，`Priority` 大者先匹配（等价原 `else if` 链顺序）；判定方法可写在本类或复用 `DataUtility.MatchXxx` |
| 跨 Mod 反射类型 | `TypeName = "PipeSystem.ProcessDef"` + `MayRequire = TweakDatabase.VEF`（反射取不到则跳过） |
| 兜底类型 | `IsFallback = true`，同 Def 类型下无规则命中时生效（如 `MiscThingData`） |
| 并列挂载 | `Exclusive = false` 允许一个 Def 挂多个 Data；默认 `true` = 命中即停 |

`TweakDatabase` 不需要任何改动。注册失败（Match 方法找不到、缺无参构造等）会在启动日志报 `TweakRegistry 注册失败：…`。

### 3.6 注册子项（可选）

```csharp
class ThoughtData : TweakData<ThoughtData>, ISubItemHost
{
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not ThoughtDef d || d.stages.NullOrEmpty()) yield break;
        for (int i = 0; i < d.stages.Count; i++)
        {
            if (d.stages[i] == null) continue;
            var sd = new ThoughtStageData { index = i };
            sd.SetParentTweak(parent, SettingType.ThoughtStage, tweaked);
            yield return sd;
        }
    }
}
```

实现方负责设置 `index`、调用 `SetParentTweak`、跳过空条目；`TweakDatabase` 自动收集，初始化流程不用加分支。

### 3.7 本地化

在 `Languages/English/Keyed/` 与 `Languages/ChineseSimplified/Keyed/` 对应 XML 里加 key（`<MST.名称>值</MST.名称>`）：

| 内容 | Key |
|------|-----|
| SettingType 页签标题 | `MST.{枚举值}` |
| `[TweakField]` 字段列标题 | `MST.{字段名}` |
| 私有 enum 分类名 | `MST.{枚举值}` |
| 字段 tooltip（含义不直观或带风险时加） | `MST.{字段名}Comment` |
| `ColumnStyle.Config` 触发列按钮 | `MST.{字段名}Edit`（`CheckLoc` 已认这条约定）；用 `ButtonText` 时是方法里显式写的那几个 key |

**文件映射**：多数 SettingType 一个对应一个 XML 文件（对照表见 §5.3）；子项页签与父页签共用文件（`ThoughtStage` / `HediffStage` / `TraitDegree`），`Misc` 归 `Core.xml`。新增枚举值时需创建对应新文件。

> **共享键不要重复定义**：`defLabel` / `statBases` / `pathCost` / `stackLimit` / `destroyable` / `Misc` 这类跨类型共用的键只定义一处，新类型直接复用。**先 grep 现有位置再加**（`statBases` / `Misc` 在 `Core.xml`，`defLabel` 在 `Damage.xml`，并不都在 Core）；`CheckLoc` 报跨文件重复时会以 `Core.xml` 那份为准——也就是说复制到 Core 只会让原来那处的定义被删。

## 4. 给现有 Data 类添加字段

### 4.1 三处修改

```csharp
// 1. 字段声明
[TweakField(Style = ColumnStyle.DefList)]
public List<HediffDef>? makeImmuneTo = null;

// 2. 初始化（SetDef / SetParentTweak；列表字段必须带守卫）
if (!IsFieldModified(nameof(makeImmuneTo))) makeImmuneTo ??= stage.makeImmuneTo;

// 3. 写入（Apply；用门控 helper，不要手写 if）
ApplyL<HediffDef>(nameof(makeImmuneTo), v => stage.makeImmuneTo = v);
```

写入 helper 按目标选：标量 `ApplyV` / 可空 `ApplyN` / Def 引用 `ApplyD` / 列表 `ApplyL` / 字符串 `ApplyS`；`[TweakField(StatDef = "...")]` 字段直接调 `ApplyDefStats(def)`：

```csharp
public override void Apply()
{
    if (this.def is not ThingDef def) return;
    ApplyDefStats(def);   // Stat 字段 + statBases/equippedStatOffsets 写入并双向同步
    // …其余非 stat 字段照常 ApplyV / ApplyN / ApplyL / ApplyD
}
```
参考：`FoodData.Apply()`、`StuffData.Apply()`、`RaceData.Apply()`。

### 4.2 子项目 Data

`HediffStageData`、`ThoughtStageData` 等不重写 `SetDef()`，改用 `SetParentTweak` 模式：

| 特点 | 说明 |
|------|------|
| **私有字段** | 持有子对象引用（`HediffStage? stage`），通过它读写 |
| **`SetParentTweak`** | 替代 `SetDef`，由父 Data 回调 |
| **`parentTweakId`** | 记录父项目 ID，`GetPropType()` 继承父级分类 |
| **`index`** | 子项目在父列表中的索引 |
| **身份校验** | 用 `untranslatedLabel` 与保存的 `stageLabel` 比对，防重命名后错位 |
| **空阶段** | 空 `<li></li>` 返回 null，跳过初始化 |

访问方式是 `stage.fieldName`，不是 `def.stages[i].fieldName`。

### 4.3 列表 / 复合字段接线

存档文本与剪贴板格式**只有一处定义**：`TweakData.FieldCodecs.cs` 的字段表（`PocoList<T>(...)`）。格式恒为 `字段名=值|字段名=值`（顺序即声明顺序，等于初值的字段省略，列表写成 `[a;b]`），`PocoCodec` 负责编解码与转义，任一条失败即整块拒绝。**不要再写逐类型的手写编码器。**

先按 §5.2 看能否复用现成样式（`DefList` / `StringList` / `IntList` / `StatModList` 等）。

#### A. 元素类型是普通 POCO（`SkillRange` / `TerrainThreshold` / `SkillRequirement`）

1. `TweakData.FieldCodecs.cs` 的 `Build()` 加一行 `PocoList<T>("字段1", "字段2", ...)` —— 同时完成字段表声明、落盘/读档登记、剪贴板与窗口默认实现。字段 `private` 也可（按 `FieldInfo` 读写）；需校验取值范围用 `PocoList<T>(validate, ...)`。
2. `StatColumnConfig.cs`：`ColumnStyle` 加成员 → 加进 `ValidStyleMap` 的 `FieldType.List` 行 → 加进 `IsListStyle()` → `GetString()` 的单元素摘要 `switch` 加 case（否则列表恰好一条时显示 `[1]`）。`GetCopyData()` / `TryPasteData()` 不用动。
3. 编辑器窗口（见 C）。
4. 本地化：`MST.{Style}`（枚举标签，`Core.xml`）、`MST.{字段名}`，含义不直观再加 `MST.{字段名}Comment`。
5. 测试：`Tests/SerializationCodecsTests.cs` 的 `MigratedTypes` 与 `字段声明逐个钉住` 各加一条；`Tests/FieldCodecsTests.cs` 的 `精确类型都能查到` 加 `InlineData(typeof(List<T>))`。

#### B. 类型进不了字段表（`IngredientCount` / `ThingFilter` / 多态列表）

在 `FieldCodecs.Build()` 用 `Exact(typeof(List<T>), "诊断名", ExposeXxx, ResolveXxx)` 手写一对：

- 落盘 `ExposeList(data, meta, v => 对象列表 → List<string>)`
- 读档 `ResolveList(data, meta, strList => (result, success))`，返回 `false` ⇒ 整字段标损坏、保持 Def 现状（绝不写半截）

照 `ExposeIngredientList` / `ResolveIngredientList` 抄。

#### C. 新增编辑窗口

1. 新建 `GUI/Editor/{Style}EditorWindow.cs`，继承 `ListEditorWindow<T>`，写两个构造：
   ```csharp
   public XxxEditorWindow(StatColumnConfig config, TweakData data) : base(config, data) { /* 从 config.GetRawValue 取初值 */ }
   public XxxEditorWindow(string title, List<Xxx> initialList, Action<List<Xxx>?> onSave) : base(title, onSave) { /* 子窗口调用 */ }
   ```
   `initialList` 由子类自己消费——基类只有 `(StatColumnConfig, TweakData)` 和 `(string title, Action<List<T>?>)` 两个构造。元素类型要到运行期才知道的（`DefList`）不套这个基类，直接 `EditorWindowBase` 自己管两份列表（见 `DefListEditorWindow`）。
2. 构造里把 `originalList` / `workingList` 换成**新实例**（`PocoCodec.CloneElement` 或逐字段复制），否则改到一半点取消已污染数据。
3. 实现 `AddButtonLabel` / `OnAddItem` / `DrawEntry`，需要时覆写 `IsItemValid`；行内宽度从 `rect.width` 扣掉删除按钮与间距后再按比例分配。
4. **不要覆写 `SerializeList` / `DeserializeList`** —— 元素进字段表后默认实现即完整实现；多覆写会被 `ListEditorWindowContractTests` 判成"多余转发"（确需覆写要登记进 `IntentionalOverrides`）。
5. `GUI/BalanceTweakGUI.cs` 窗口工厂表加 `{ ColumnStyle.Xxx, (c, d) => new XxxEditorWindow(c, d) }`。
6. 本地化：`MST.AddXxx`（添加按钮），选择类按钮优先复用 `MST.SelectXxx`。
7. 验收：`dotnet build` + `dotnet test` + `dotnet run --project Source/CheckLoc`。

### 4.4 多态列表（`StockGenList` / `PawnGroupMakerList`）

元素子类各异、字段不同，无法用固定列列表编辑器表达。四条不变式：

- **字段值就是真实对象**，且与 Def 的活对象永不共享实例：`SetDef` 克隆进字段，`Apply` 再克隆交给 Def，"编辑 → 再编辑 → 重复 Apply"不会互相污染。
  ```csharp
  if (!IsFieldModified(nameof(stockGenerators)))
      stockGenerators ??= StockGeneratorCodec.CloneAll(d.stockGenerators);
  ApplyL<StockGenerator>(nameof(stockGenerators), ApplyStockGenerators);
  ```
- **字段表是唯一信息源**（`StockGeneratorCodec` 逐类型 `PocoCodec.Declare(typeof(...), "字段", ...)`）。表里没有的类型按"非原版"处理：只读、编辑/移动/删除禁用、存档只留类型名占位，`Apply` 时按"类型相同 + 出现次序相同"从 Def 找回原实例放回原位置。
- **文本只出现在两个边界**（存档、剪贴板），由 codec 编码（`StockGenerator_Tag|countRange=1~4|tradeTag=ExoticMisc`，转义安全、非 XML）；编辑路径上没有文本格式，窗口直接读写对象字段。
- `Apply` 写完列表后对每个元素调 `ResolveReferences(def)`（`trader` 是 `[Unsaved]`，必须靠它写回）。

`PawnGroupMaker` 走同一模式但没有多态（原版无子类）：

- 字段声明仍只有一处：`PocoCodec.Declare(typeof(PawnGenOption), "kind", "selectionWeight")` + `PocoList<PawnGroupMaker>(...)`；`options` / `traders` / `carriers` / `guards` 是 `Kind.ComplexList`，**元素类型必须先声明**，否则被判 `Unsupported` 后静默不读不写。
- 窗口：`PawnGroupMakerListEditorWindow` + `PawnGroupMakerEditorWindow`，子列表打开 `SGComplexListEditorWindow`；表单渲染共用 `PocoFieldUi`。
- `Data/PawnGroupMakerCodec.cs` 只负责克隆、`MergeWithDef`（非原版子类按"类型 + 出现次序"找回，找不到丢弃并告警）、行摘要，**不含字段表**。
- 写回 Def 前有一道 `Sanitize`：没有 `kindDef` 的兵种池、没有 `kind` 的 `PawnGenOption` 会在袭击生成期 NRE，必须丢弃（字段里仍保留用户填的内容）。
- 子列表删光时交**空列表**而非 `null`。

实现细节读这几处：`Data/StockGeneratorCodec.cs`、`GUI/Editor/PocoFieldUi.cs`（字段标签必须用显式字面量 —— CheckLoc 是静态正则，看不见拼接出来的键）、`Tests/StockGeneratorCodecTests.cs`（"原版加类型 / 加字段"的第一道警报）、`Tests/TweakDataSerializationTests.cs` 的 `testStockGen_*`。

### 4.5 镜像列（`IsMirror`）与 Display 列

**镜像列**：值属于另一行（武器行的"伤害 / 穿透"其实是关联弹丸的副本）。标 `IsMirror = true` 并遵守两条：

- **编辑即编辑源行**：写时把值推到源行、把修改标记转移到源行，本行不留标记（`WeaponData.PushWeaponEditsToProjectile`），否则本行的重置按钮点了没反应。
- **`EqualsData` 跳过它**：源行一改，共享它的副本会被派生同步刷新，"我没改、但副本变了"是常态；参与相等判定会让脏行常驻已修改表。

**原始快照**（`TweakDatabase.GetOriginal`）是只读基准，任何派生同步都不许写它：

- 建快照期间（`TweakDatabase.buildingOriginals`）跨行读取走原始层；**不要用 `SetDef(tweaked: false)` 反推**（自检探针 `MakeProbe` 也用 false，它要读当前 def）。
- `ProjectileData.RefreshSharedWeapons` / `BuildingData.RefreshTurretGun` 跳过快照，只刷活实例。
- 从快照克隆出的行在 `EnsureDisplayValues` 里补一次镜像同步（`WeaponData.SyncMirrorsFromProjectile`），因此 `Clone()` 必须清 `displayValuesEnsured`（基类 `TweakData.Clone()` 统一清零）。
- 强度刻度（`GetEquipPower`）同样取快照当基准：原版武器 = 100，共用同一颗弹丸的武器一起变。

**Display 列**（`ColumnDataType.Display`）不落盘、不参与 `Apply`，一律**按需计算**：

| 规则 | 说明 |
| --- | --- |
| `SetDef` 只填镜像与可编辑字段的原值 | 不跑 DPS 等派生计算 |
| 派生列在**进入页签时**全量算完 | 入口 `TweakUtility.AdvanceDerive`，按每帧 6ms 预算逐行 `EnsureDisplayValues()`，显示进度条 |
| 算完置 `displayValuesEnsured = true` | `Apply()` 结束置回 false（参考值按当前 def 现算） |
| 只有原始快照需在 `Apply` 前定格 | `PrepareDeferredDisplayValues` 只遍历 `tweakDatas.Keys` |

- **不要在 `UpdateFilters` 里补算**：那是全表遍历，而表格按行列双虚拟化，一屏只有几十格；全量推导只能走 `AdvanceDerive`（只有它分帧、有进度条）。`StatColumnConfig.EnsureForDisplay` 保留但只作 O(1) 兜底。
- `GenValues()`（镜像 + DPS + 强度全量重算）只给"数据真的变了"的路径用：`Apply` / 弹丸推送 / 招式编辑 / 自检。新增 Display 列 = 字段 + `EnsureDisplayValues` 里的计算 + `Apply` 末尾置脏。
- 关联入口列（`Link` / `PawnWeaponLinks` 等）不进 `EnsureForDisplay`：字段永远为 null，点了才查。

**行标题、搜索串与展示属性**（`TweakData.label` / `searchString` / `propType` / `uiIcon` / `uiIconColor` / `desc`）同样是不落盘、不参与 `Apply` 的派生值。策略是**首读才算 + 打开页签时分帧预热**，两条缺一不可：只懒不预热，卡顿会从启动挪到滚动上。

| 规则 | 说明 |
| --- | --- |
| 首次读取时才算（`ResolveLabel` / `ResolveSearchString` / `GetPropType` / `ResolveUiIcon` / `ResolveUiIconColor` / `ResolveDesc`） | 原先每个 `SetDef` 都给全库四万行算一遍：label 要翻译、`propType` 要跑分类判定、tooltip 要拼字符串 —— 而绝大多数行根本不会被显示 |
| **滚动路径上不许首读** | 前五个值由 `AdvanceDerive` 分帧预热（`TweakData.WarmDisplayProps`，每帧 6ms、带进度条），筛选与绘制只剩字段读。**不能只靠"用到才算"**：新滚进视野的行会在滚动那一帧当场解析，卡顿正好落在手指上 |
| `desc` 例外：**悬停才算** | 绘制行的 `if (Mouse.IsOver(rowRect))` 里才登记 tooltip，未悬停不生成字符串。`TipRegion` 本来也是"不悬停就不显示"，结果等价；它不参与预热（整页签白付一遍拼接没意义） |
| def 未绑定时**不缓存** | 此时只能算出兜底值，缓存下来会让之后补上 def 的行永久停在兜底标题 / 兜底分类 |
| 清缓存只走 `ResetPropsCache()` | 由 `SetProps` 调用（所有 `SetDef` / `SetParentTweak` 的必经之路，写在基类 `SetDef` 会漏掉重写者）与 `Clone` 调用。六个值一起清：`searchString` 由 `label` 拼出来，图标 / 描述也由 def 派生，单清一个就留下错位 |
| 要定制就**覆写 `Resolve*`** | `propType` / `uiIcon` / `uiIconColor` / `desc` 是**只读**属性，没有 setter —— 没有"顺手赋值把懒加载变回急加载"这条路。现有覆写：`FactionData`（FactionIcon / 派系色）、`MemeData` / `XenotypeData` / `ThoughtData`（Icon）、`AbilityData`（VEF icon + `GetTooltip`）、`RecipeData`（UIIcon / 产物图标 / ProcessDef）、`PawnKindData`（借种族那一行） |
| `propType` 只有一个来源 | `GetPropType`。原先"基类先算一次、`HeavyEquippableDefData` 再修正一次"的两步写法已删除 —— 首次读取时字段早已回填，机载档位自然判对 |
| 子项分类继承父级 | `HediffStageData` / `TraitDegreeNodeData` / `ThoughtStageData` 的 `GetPropType` 自己查 `parentTweakId`（父级也是懒的，链式按需），不再在 `SetParentTweak` 里抄一份 |
| 例外：**跨行索引一律在建表阶段建** | `GetPropType` 只读不写。原先 `raceDatas` / `projectileDatas` / `turretBuildingDatas` 是在各自 `GetPropType` 里顺带写的 —— 靠"初始化期每行都会被求值一次"活着，改懒之后谁先被读谁才写：武器解析弹丸时表是空的（镜像全空、强度除零）、判炮塔时表也是空的（炮塔枪回落成内置武器）。现在统一由 `TweakDatabase.BuildLookupIndices` 在分类之后、建快照之前填好 |
| 索引里的 `TweakID` 必须**现拼**（`new TweakID(def.defName, type)`） | `data.id` 是在随后的 `SetDef` 里才赋值的，`BuildLookupIndices` 跑在那之前，此刻它还是 `default`（defName 为空、settingType 为 `None`）。用它建表会把所有条目塌缩到同一个 `":None"` 键：武器解析不到弹丸（**DPS 全 0**）、炮塔枪拿到空链接（点一下 `KeyNotFoundException`）。`JumpToData` 现在也把空 defName / 无对应行当死链处理，不再抛 |
| 枪 ↔ 炮塔的链接在 `WeaponData.SetDef`（`LinkTurret`） | 与 `PawnKindData.LinkRace` 同一个理由：它是"枪挂在哪座建筑上"的跨行关系，不是展示值，也不该由"读分类"顺手产生 |
| `Def.LabelCap` 可能返回 `null` | 原版在 `label` 为空时**不回落** defName，基类 `ResolveLabel` 自己回落 |
| 任务名由 `QuestNameResolver` 现拼 | 只认 `questNameRules` 里的纯文本规则（`Rule_String`），不掷骰子、不碰 slate；任何 token 拿不到选项就整条放弃，退回 defName |

**打开窗口分两段准备**（期间 `InitInProgress` 为 true，`DoEditorContents` 画进度条后 return）：

| 段 | 入口 | 规模 | 预算 | 进度 |
| --- | --- | --- | --- | --- |
| ① 建行缓存（克隆） | `AdvanceInit` | 全库 4 万级，一次性 | 每帧 6ms（按时间，保底 200 行） | 0~90% |
| ② 页签派生值 + 展示属性预热 | `AdvanceDerive` | 当前页签几百~几千行，每次切页签 | 每帧 6ms（同一个 `PrepareBudgetMs`） | 90~100% |

约束：没建完不许 `UpdateFilters`（`cachedData` 不全，会缺行）；小数据包必须一帧做完（否则进度条闪一下）；`DrawInitProgress` 要还回 `Text.Font` / `Text.Anchor`（提前 return 会把 IMGUI 全局状态漏给下一帧）；**两段都按时间给预算（`PrepareBudgetMs`），不按行数**。① 原先是"每帧固定 2000 行"，问题在于 `4µs/行` 只是某个机器某个整合包下的数（克隆成本还随类型字段数、`List` 长度、字典扩容、GC 变化），慢一点 2000 行就是 16~20ms，整帧被顶到下一个 vsync 档：22 帧于是变成 0.7s 而不是 0.37s。按时间切则不论机器快慢都不超额，代价是慢机器上帧数变多、总墙钟时间随之变化。要调预算时看日志里的 `µs/行` 换算即可。

> 派生值置脏时只有**当前页签**重推（`CombatPower.InvalidateRows` 判 `type == TweakUtility.CurrentType`）。列定义（反射 + `Translate`，约 20~30ms/类型）也在第 ② 段首帧预热。> `ApplyFullReset`（"完全重置"）仍是同步重建。
> 克隆成本在 `TweakData<T>.DeepCopyFields`：只有 `List<T>` 需深拷贝，故那里有 `if (!meta.IsList) continue;`。
> `[BalanceTweak][perf]` 日志走 `TweakLog.Message`，受「总体设置 → 诊断 → 冗杂日志」控制（默认关）；`Log.Error` / `Log.Warning` 不走这里，永远输出。

### 4.6 关联列（`RaceLinks` / `BodyLinks` / `WeaponLinks`）

反向关联（"谁在用我"）不能用 `Link` 表达（那是一对一）。统一走"只读触发列 + 打开时实时反查"：

| 列 | 所在 Data | 点击后列出 | 反查方法 |
| --- | --- | --- | --- |
| `RaceLinks` | `BodyData` | 使用该 `BodyDef` 的种族 | `BodyData.FindRaceLinks` |
| `RaceLinks` | `BodyPartData` | 身体含该部位的种族 | `BodyPartData.FindRaceLinks` |
| `BodyLinks` | `BodyPartData` | 部位树含该部位的 `BodyDef` | `BodyPartData.FindBodyLinks` |
| `WeaponLinks` | `ProjectileData` | 发射该投射物的武器 | `ProjectileData.FindWeaponLinks` |
| `PawnWeaponLinks` | `PawnKindData` | 该兵种能抽到的手持武器 | `PawnKindData.FindWeaponLinks` |
| `PawnWeaponLinks` | `RaceData` | 该种族名下所有兵种能抽到的武器（并集） | `RaceData.FindWeaponLinks` |

三条不变式：

- **字段只是触发器，不存值**：值永远 null，`GetString` 直接返回按钮文字，不在绘制阶段扫 Def。
- **打开窗口时反查一次**，结果交给 `TweakLinksWindow`（绘制阶段不查 Def）；不缓存，数据可能刚被改过。
- **只列真有数据行的对象**（`TweakDatabase.GetOriginal(id) != null`），否则是死链接。

**新增一个 Links 样式要动 6 处**：`ColumnStyle` 枚举（末尾追加）→ `ValidStyleMap` 的 `FieldType.TweakID` 行 → `StatColumnConfig.IsTriggerStyle`（派生值推导与列过滤都靠这一处跳过触发列，漏了会白算整页战斗力、并得到一个永远为空的过滤条件）→ `GetString` 的按钮文字分支 → `BalanceTweakGUI.DrawDisplayCell` 的点击分发 → `IsPaintableColumn` 的排除列表（只对非 `Display` 的触发列必要，其余已被 `columnDataType` 挡掉）；再补 `MST.XxxLinks` 与字段级 `MST.{字段名}Comment`。

Fortified 的三个同类触发列（`MechWeaponFilter` / `RelayConfig` / `TurretConfig`）走的是另一套 —— **一个只读触发列 + 一个配置窗口**，见 §4.8。它们的字段同样是 `TweakID?` 占位，真数据在若干 `NotAColumn` 字段里。

> 兵种这条反查的是**规则**：`PawnKindDef` 没有"武器"数据，武器按 `weaponTags` + `weaponMoney` 现抽，只能扫全库套同一套判定（标签交集 / 预算上界 / `generateAllowChance > 0` / `weaponStuffOverride` 的材料可兼容）。预算取上界，攻击列取期望（5 档采样）；功能武器（烟雾 / EMP）会列出，但攻击列按 `harmsHealth` 排除。
> 种族页与兵种页共用 `PawnKindData.CollectWeaponLinks`（逐兵种各扫一遍在 `Human` 上会变成十几万次重复遍历）。

### 4.7 绘制收尾：IMGUI 全局状态会漏进 tooltip

`TooltipHandler.TipRegion` 只是**登记**字符串，真正的绘制发生在整个 OnGUI 末尾（`ActiveTip.DrawTooltip`）。那里只设 `Text.Font`，**换行与对齐沿用绘制这一刻的全局值**。所以窗口在绘制过程中改过的 `Text.WordWrap` / `Text.Anchor` 会漏到 tooltip 上，症状是"左侧名称列的 tooltip 有时不换行 / 居中"，取决于最后一格画的是什么。

- `BalanceTweakGUI.DrawData` 在入口记下 `Text.WordWrap` / `Text.Anchor`，出口原样还原；中间的数据格（`Text.WordWrap = false`）与单元格对齐（`MiddleCenter`）不再外溢。
- 局部改动一律 **保存 → 改 → 还原**（`DrawNameRow` 的标签、`ConfigWindow` / `MechWeaponFilterWindow` / `RelayConfigWindow` / `TurretConfigWindow` 的正文都是这么写的），不要硬写成 `= true`：那在调用方本来是 `false` 时会把状态改脏。
- 同理适用于 `Text.Font` / `GUI.color`：`DrawInitProgress` 已经这么处理。

### 4.8 触发列 + 配置窗口（`ColumnStyle.Config`）

一个 Def 上有十几到几十个相关字段时（AO 的建筑：9 个 Comp + 1 个扩展、56 个数值），逐列展示会把表格挤爆，而每行只命中其中两三个。做法是**表格里只留一个只读触发列，字段全部 `NotAColumn`，点开一个窗口统一编辑**。

**新增一种配置 = 1 个窗口类 + 1 个字段声明，不动任何 switch。** 列样式只有一个 `ColumnStyle.Config`，窗口类型由字段自己指定。

> 早期做法是"每种配置加一个 `ColumnStyle` + 一个窗口"，连加 6 种之后，`ColumnStyle` 枚举、`ValidStyleMap`、`IsTriggerStyle`、`GetString`、`DrawDisplayCell`、`IsPaintableColumn` 六处各有一份六个分支的清单。新增第 7 种时漏一处就静默出错：漏 `IsTriggerStyle` = 整页战斗力白算一遍 + 多一个永远为空的过滤条件；漏 `DrawDisplayCell` = 按钮画出来但点了没反应。收敛成一个 `Config` + `Editor` 属性后，"接线"这件事从六处变成零处。

#### 字段声明

```csharp
/// <summary>只读触发列：打开大气建筑配置窗口。</summary>
[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
    Editor = typeof(AtmosphereBuildingConfigWindow),
    MayRequire = TweakDatabase.AO,
    Hidden = nameof(HiddenIfNoAtmosphereComp))]   // 一个相关 Comp/扩展都没有的行整格留白
public TweakID? atmosphereConfig = null;

[TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
public float? fanMaxPowerWatts = null;
```

- 字段类型必须是 `TweakID?`（占位，永远不存值）。
- `Editor` 必须是本程序集的 `Window` 子类、且只有一个 `(TweakData)` 构造。启动期校验一次，写错是一条日志，不会等玩家点下去才抛。**不许指向可选 Mod 的类型** —— 那会编出 typeref（`AssemblyReferenceTests` 会拦）。
- 按钮文字默认 `MST.{字段名}Edit`（例：`atmosphereConfig` → `MST.atmosphereConfigEdit`），`CheckLoc` 把它认作代码引用，不会成孤儿键。要按行换词时加 `ButtonText = nameof(静态方法)`（唯一用例：中继配置没有任何 Comp 时说"添加"）。

#### 窗口：声明分节

```csharp
public class AtmosphereBuildingConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        // 参数：节标题（null = 不画标题）、出现条件（null = 总显示）、行
        ConfigSection.Of("MST.aoFanSection", HasFan,
                ConfigRow.Float(nameof(BuildingData.fanMaxPowerWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.fanRatedFreeAirFlow)),
                ConfigRow.Bool(nameof(BuildingData.fanAllowOutputAdjustment)))
            .Note("MST.aoFanNote"),   // 节尾灰色说明

        ConfigSection.Of("MST.aoJetSection", HasJet,
            ConfigRow.Int(nameof(BuildingData.jetColumnHeight), 1),
            ConfigRow.Enum(nameof(BuildingData.jetPowerSource)),       // 下拉（OptionsList / EnumType）
            ConfigRow.Def(nameof(BuildingData.jetProductDef)),         // Def 选择器
            ConfigRow.Percent(nameof(BuildingData.jetOxygenPercent))), // 界面 0~100，字段存 0~1
    };

    public AtmosphereBuildingConfigWindow(TweakData data)
        : base(data, "MST.atmosphereConfig", "MST.atmosphereConfigIntro", Sections,
               preferredHeight: 780f, noteKey: "MST.atmosphereConfigNote", validate: Validate)
    {
    }

    // 出现条件必须是**方法组**；不要在 lambda 里碰可选 Mod 的类型（会编出 typeref）。
    private static bool HasFan(TweakData d) => AoHelper.FindComp(d.def, AoHelper.FanProps) != null;

    private static IReadOnlyList<ConfigIssue> Validate(TweakData d) { /* … */ }
}
```

行的 `labelKey` 默认 `MST.{字段名}`、`commentKey` 默认 `MST.{字段名}Comment` —— 与表格列标题同一套约定，`CheckLoc` 会自动要求它们存在。

#### 窗口能做的事

| 行 | 控件 | 写回 |
|---|---|---|
| `ConfigRow.Int` / `Float` | 内联文本框（回车 / 失焦 / 关窗才提交） | `ApplyValue` |
| `ConfigRow.Percent` | 同上，界面 0~100、字段 0~1 | `ApplyValue` |
| `ConfigRow.Bool` | 复选框 | `ApplyValue` |
| `ConfigRow.Enum` | FloatMenu 下拉（选项来自 `OptionsList`，没有则用 `EnumType` 成员名） | `CheckAndApply` |
| `ConfigRow.Def` | 按钮 → `DefSelectionWindow` | 由选择器 |
| `ConfigRow.String` | 内联文本框 | `CheckAndApply` |
| `ConfigRow.List` | 按钮 → 该列样式对应的列表窗口（复用表格那张 `EditorWindowFactory`） | 由列表窗口 |
| `validate` | 窗口底部「配置检查」：错误红 / 提示黄 / 无问题绿 | — |

四条不变式：

- **写入只有一条路径**：全部走 `StatColumnConfig.ApplyValue` / `CheckAndApply`，与表格编辑同一条三态 / 重置 / `ResearchUnlocks` 失效 / `CombatPower.Invalidate` 路径。**不要**在窗口里直接写 Def 字段 —— 那样改了不进存档、重置也清不掉。
- **节的 `Visible` 就是"这个 Def 有没有对应 Comp/扩展"**，绘制时逐行求值（几十次反射，可接受）。要"整个触发列在不适用行留白"另配 `Hidden`。
- **同一份声明可以服务多个宿主**（种族 / 建筑 / 服装都挂同一组 Comp）：`ConfigWindow` 构造时按字段名解析一遍，取不到的整行跳过、不占高度。所以 `Visible` 通常写成 `d => FindComp(d.def) != null && Has(d, nameof(Xxx.某字段))` —— 前半个判"这个 Def 有没有这个 Comp"，后半个判"这个宿主有没有这个字段"。
- **窗口高度不用传死值**：内容高度在绘制前算好再开滚动区（旧实现传死 `viewHeight`，字段一多就滚不到底）。`preferredHeight` 只影响窗口初始高度。
- **IMGUI 全局状态**：改 `Text.Font` / `Text.Anchor` / `Text.WordWrap` 一律"保存 → 改 → 还原"（见 §4.7）。覆写 `DrawBody` 时自己也要守这条。

> 现有 6 个触发列已全部迁到 `ColumnStyle.Config`。四个是 `ConfigWindow` 子类，只剩声明：`APSConfigWindow` 19 行 / `ShieldConfigWindow` 71 行 / `SweepConfigWindow` 58 行 / `RelayConfigWindow` 108 行（六节，服务种族 / 建筑 / 服装三个宿主）。
>
> **`turretConfig` 与 `mechWeaponFilter` 仍是手写窗口**，不是漏迁。它们的行不是"TweakData 上的一组平铺字段"：炮塔的节数与行数由 `turretSlots` 列表的**当前长度**决定，每行的读写目标是窗口里的工作副本 `slot.warmingTime`，改完整份列表再 `ApplyValue` 一次；武器过滤则要一个"从武器库里挑"的自定义选择器和一块只读的候选预览。给 `ConfigWindow` 补上这两样（动态节 + 行级取值/写回委托）等于把它改成通用表单引擎，P3 那种"九个静态节、五十六个平铺字段"的场景却要为这层抽象买单。收益不抵成本，所以保留手写。

## 5. 参照表

### 5.1 TweakField 属性

| 参数 | 用途 | 示例 |
|------|------|------|
| `Style` | 列显示样式（见 §5.2） | `ColumnStyle.Float` |
| `EnumType` | Enum 类型（下拉菜单） | `typeof(FoodPreferability)` |
| `StatDef` | 关联 StatDef 名称 | `"Nutrition"` |
| `MayRequire` | 需要某 Mod 才显示 | `"ceteam.combatextended"` |
| `IsEquippedStat` / `IsStuffFactorStat` / `IsStuffOffsetStat` | stat 写入位置（装备 / 材料倍率 / 材料加成） | `true` |
| `DataType` | `Field`（可编辑）或 `Display`（只读） | `ColumnDataType.Display` |
| `IsMirror` | 值属于另一行，不参与 `EqualsData`（见 §4.5） | `true` |
| `Available` | **列级**显示条件方法名，按第一行求值一次 | `"AvailableIfNotHumanlike"` |
| `Hidden` | **按行**留白条件方法名（true = 该格不显示），语义是"不适用" | `"HiddenIfHumanlike"` |
| `GetAbstract` | 获取抽象值方法名 | — |
| `OptionsList` | 静态列表字段名（动态选项） | `"ResearchTabDefs"` |
| `NotAColumn` | 只落盘、不出现在表格里（配合自定义窗口，`AllColumn` 排除） | `true` |
| `Editor` | `ColumnStyle.Config` 触发列点开的窗口类型（本程序集的 `Window`，需 `(TweakData)` 构造） | `typeof(MyConfigWindow)` |
| `ButtonText` | 触发列按钮文字的方法名（`static string Xxx(TweakData)`），不写就用 `MST.{字段名}Edit` | `nameof(RelayConfigButtonText)` |

> `OnChange` / `Default` 已标记 `[Obsolete]`，新代码不要用：逻辑放 `Apply` / 默认值放 `SetDef`。

### 5.2 ColumnStyle ↔ 字段类型 ↔ 编辑窗口

| ColumnStyle | C# 类型 | 编辑方式 / 窗口 |
|------------|---------|----------------|
| `Float`（默认） | `float?` | 内联文本框 |
| `Int` | `int?` | 内联文本框 |
| `Bool` | `bool?` | 内联复选框 |
| `String` | `string?` | 弹窗 `StringEditorWindow` |
| `Enum` | `Nullable<Enum>` | FloatMenu 下拉（需 `EnumType` 或 `OptionsList`） |
| `Flags` | `Nullable<Enum>`，带 `[Flags]` | 弹窗 `FlagsEditorWindow` |
| `Prec` | `float?` | 内联文本框 + %（值/100） |
| `Range` | `FloatRange?` / `IntRange?` | 内联文本框，写 `min~max` |
| `Curve` | `SimpleCurve?` | 弹窗 `CurveEditorWindow` |
| `Link` | `TweakID?` | 内联跳转链接（`Display`） |
| `RaceLinks` / `BodyLinks` / `WeaponLinks` / `PawnWeaponLinks` | `TweakID?` | 弹窗 `TweakLinksWindow`，只读触发列（`Display`，见 §4.6） |
| `MechWeaponFilter` / `RelayConfig` / `TurretConfig` | `TweakID?` | 弹窗 `MechWeaponFilterWindow` / `RelayConfigWindow` / `TurretConfigWindow`，只读触发列（`Display`，Fortified） |
| `Config` | `TweakID?` | 弹窗由 `[TweakField(Editor = typeof(...))]` 指定；`ConfigWindow` 子类（声明式分节）或手写窗口。只读触发列（`Display`），见 §4.8 |
| `TurretSlotList` | `List<TurretSlotRecord>?` | 不出列（`NotAColumn`），由 `TurretConfigWindow` 按槽分节编辑 |
| `DefSelector` | `T? where T : Def` | 弹窗 `DefSelectionWindow` |
| `StatModList` | `List<StatModifier>?` | 弹窗 `StatModListEditorWindow` |
| `CapModList` | `List<PawnCapacityModifier>?` | 弹窗 `CapModListEditorWindow` |
| `DamageModList` | `List<DamageFactor>?` | 弹窗 `DamageModListEditorWindow` |
| `ExtraDamageList` | `List<ExtraDamage>?` | 弹窗 `ExtraDamageListEditorWindow`（`armorPenetration = -1` 是原版"自动折算"语义） |
| `DefList` | `List<T>? where T : Def` | 弹窗 `DefListEditorWindow`（添加/删除/替换） |
| `HediffGiverList` | `List<HediffGiver>?` | 弹窗 `HediffGiverListEditorWindow` |
| `MentalStateGiverList` | `List<MentalStateGiver>?` | 弹窗 `MentalStateGiverListEditorWindow` |
| `SkillGainList` | `List<SkillGain>?` | 弹窗 `SkillGainListEditorWindow` |
| `SkillReqList` | `List<SkillRequirement>?` | 弹窗 `SkillReqListEditorWindow` |
| `TraitReqList` | `List<TraitRequirement>?` | 弹窗 `TraitReqListEditorWindow`（trait + degree） |
| `AptitudeList` | `List<Aptitude>?` | 弹窗 `AptitudeListEditorWindow` |
| `GeneticTraitList` | `List<GeneticTraitData>?` | 弹窗 `GeneticTraitListEditorWindow` |
| `StringList` | `List<string>?` | 弹窗 `StringListEditorWindow`（添加/编辑/删除） |
| `ThingSetTags` | `List<string>?` | 复用 `StringListEditorWindow`；保存时去空白 / 去重 |
| `IntList` | `List<int>?` | 弹窗 `IntListEditorWindow` |
| `ThingDefCountList` | `List<ThingDefCountClass>?` | 弹窗 `ThingDefCountListEditorWindow` |
| `IngredientList` | `List<IngredientCount>?` | 弹窗 `IngredientListEditorWindow` |
| `IngredientFilter` | `ThingFilter?` | 弹窗 `IngredientFilterEditorWindow` |
| `ProcessIngredientList` / `ProcessResultList` | `List<ProcessIngredientItem>?` / `List<ProcessResultItem>?` | 各自 EditorWindow（PipeSystem / VEF） |
| `BodyPartTree` | `List<string>?` | 弹窗 `BodyPartTreeEditorWindow`：字段存前缀序遍历编码，经 `BodyPartTreeCodec` 与 `BodyDef.corePart` 互转 |
| `BiomePlantList` / `BiomeAnimalList` | `List<BiomePlantRecord>?` / `List<BiomeAnimalRecord>?` | 各自 EditorWindow（+ commonality） |
| `FishChanceList` | `List<FishChance>?` | 弹窗 `FishChanceListEditorWindow` |
| `XenotypeChanceList` | `List<XenotypeChance>?` | 弹窗 `XenotypeChanceListEditorWindow` |
| `DiseaseList` | `List<BiomeDiseaseRecord>?` | 弹窗 `DiseaseListEditorWindow` |
| `WeatherCommonalityList` | `List<WeatherCommonalityRecord>?` | 弹窗 `WeatherCommonalityListEditorWindow` |
| `TerrainThresholdList` | `List<TerrainThreshold>?` | 弹窗 `TerrainThresholdListEditorWindow` |
| `StockGenList` | `List<StockGenerator>?` | 弹窗 `StockGeneratorListEditorWindow`（见 §4.4） |
| `PawnGroupMakerList` | `List<PawnGroupMaker>?` | 弹窗 `PawnGroupMakerListEditorWindow`（见 §4.4） |
| `FactionCurveList` | `List<FactionCurve>?` | 弹窗 `FactionCurveListEditorWindow`；曲线文本 `[x~y;x~y]` |
| `SkillRangeList` | `List<SkillRange>?` | 弹窗 `SkillRangeListEditorWindow`（skill + IntRange） |
| `BackstoryTraitList` | `List<BackstoryTrait>?` | 弹窗 `BackstoryTraitListEditorWindow`（trait + degree） |
| `PossessionList` | `List<PossessionThingDefCountClass>?` | 弹窗 `PossessionListEditorWindow`（Def + 数量区间） |
| `MutatorChanceList` | `List<MutatorChance>?` | 弹窗 `MutatorChanceListEditorWindow`（mutator + 几率 + 是否必需） |
| `UnlockList` | `List<Def>?` | 弹窗 `ResearchUnlockWindow`，**只读派生列**（见 §6.2） |

> 所有列表编辑器删光保存都走显式 Cleared 路径（`ListEditorWindow.CommitResult` → `ClearValue`），否则空列表被判 `Unset`、`ApplyL` 静默跳过，旧项删不干净。

### 5.3 SettingType ↔ XML 文件

| SettingType | 文件 | SettingType | 文件 |
|---|---|---|---|
| —（通用 UI，无对应枚举值） | `Core.xml` | `Terrain` | `Terrain.xml` |
| `Race` | `Race.xml` | `Recipe` | `Recipe.xml` |
| `Apparel` | `Apparel.xml` | `Misc` | `Core.xml` |
| `Weapon` | `Weapon.xml` | `Damage` | `Damage.xml` |
| `MeleeTool` | `MeleeTool.xml` | `BodyPart` | `BodyPart.xml` |
| `Ability` | `Ability.xml` | `BodyDef` | `BodyDef.xml` |
| `Stuff` | `Stuff.xml` | `Biome` | `Biome.xml` |
| `Building` | `Building.xml` | `Trader` | `Trader.xml` |
| `Projectile` | `Projectile.xml` | `Xenotype` | `Xenotype.xml` |
| `Thought` / `ThoughtStage` | `Thought.xml` | `ArrivalMode` | `ArrivalMode.xml` |
| `PawnKind` | `PawnKind.xml` | `RaidStrategy` | `RaidStrategy.xml` |
| `Gene` | `Gene.xml` | `Backstory` | `Backstory.xml` |
| `Meme` | `Meme.xml` | `Landmark` | `Landmark.xml` |
| `Incident` | `Incident.xml` | `WeaponTrait` | `WeaponTrait.xml` |
| `Quest` | `Quest.xml` | | |
| `Research` | `Research.xml` | | |
| `Weather` | `Weather.xml` | | |
| `Faction` | `Faction.xml` | | |
| `Food` | `Food.xml` | | |
| `Hediff` / `HediffStage` | `Hediff.xml` | | |
| `Trait` / `TraitDegree` | `Trait.xml` | | |
| `HeavyEquipment` | `Heavy.xml` | | |

新增 SettingType 枚举值时需同时建对应 `{名称}.xml`（至少写 `MST.{枚举值}` 这条页签标题）。另有不绑定枚举值的功能专用文件（如 `Turret.xml` 的多炮塔窗口文案），按功能命名即可。

**页签顺序不在枚举里**：下拉顺序的唯一来源是 `SettingType.cs` 的 `TabOrder` 数组，与枚举声明顺序无关 —— 想调顺序只挪那里的行。新增枚举值除了建 XML，还必须往 `TabOrder` 追加一行（漏了页签不出现，`SettingTypeOrderTests` 报红）。`IsTabVisible`（缺可选 Mod 时整项隐藏）只做可见性过滤，与顺序无关。

## 6. 领域要点

### 6.1 炮塔枪的 DPS：节奏在建筑上

炮塔的"枪"只是挂件，`Building_TurretGun` 的射击节奏**完全由建筑决定**，不能按普通枪算：

| 量 | 取法（`GenRangeDPS`） | 可编辑字段（`BuildingData`） |
|---|---|---|
| 冷却 | `def.building.turretBurstCooldownTime >= 0` 用它，否则回落枪的 `cooldown` / verb 默认 | `turretBurstCooldownTime`（`Prec`） |
| 预热 | **总是** `def.building.turretBurstWarmupTime.Average`（`Verb.TryStartCastOn` 的 warmup 只在 `CasterIsPawn` 时生效） | `turretBurstWarmupTime`（`Range`，`min~max`） |

两个坑：

- 4 个原版炮塔枪（`Gun_ChargeBlasterHeavyTurret` / `Gun_InfernoCannonTurret` / `Artillery_AutoMortar` / `Artillery_Mortar`）既无 `RangedWeapon_Cooldown`、verb 也没设 `defaultCooldownTime`。漏掉建筑冷却 → `cd` 为 null → `GenRangeDPS` 提前 return → 这几行**没有 DPS / 强度**。另有 3 个枪与建筑值不一致（AutoMiniTurret 5.1/2.5、Rocketswarm 3.5/0.1、Tactical 4.8/2.4），游戏里用建筑值。
- 建筑改完要主动刷挂载的枪（`BuildingData.RefreshTurretGun`），否则武器页签停在旧值。触发判据是**预热 / 冷却任一**被改动，且必须比三态多判 `forceWriteAll` / `forceWriteFields`（重置是先写回原值再清标记，那一刻三态已是 Unset）。
- `turretBurstWarmupTime` 清空走 `ApplyV` 的 `Revert`（def 上不可空，没有"写成 null"这个状态）。

> `ShootingAccuracyTurret` 与手持武器的 `ShootingAccuracyPawn` 均未乘入 —— "射手因子统一视为 1"是这张表的既有口径，要改就两边一起改，否则炮塔与手持武器不可比。
>
> **强度（`equipPower`）对退化输入必须收口**：它拿 DPS 开方、再除以电荷步枪基准，任何 NaN / ∞ 都会一路传下去，最后在界面上显示成 `NaN` / `∞`，而 DPS 列看上去还是正常的。已经堵死的口子：① 分母 `aim × warmupMult + cd + 连发间隔` 必须 `> 0`（`burstShotCount` 为 0 也算退化）；② 基准（电荷步枪）算不出正数时退回常量 `FallbackChargePower`；③ 近战刻度的分母是"参考武器 − 空手"两个独立统计值之差，≤ 0 时近战贡献记 0；④ 每个 DPS 字段与最终强度都用 `FiniteOrZero` 收口。新增乘除项时照这个套路走。

### 6.2 研究解锁（派生列，不是字段）

1.7 没有"解锁列表"字段（`ResearchProjectDef.UnlockedDefs` 是 getter），所以"改一个研究解锁什么" = 改**别的 Def**，研究行上不存第二份真相。`Data/ResearchUnlocks.cs` 按同一份判定反查全库：

| # | 来源 | 判定 | 登记 |
|---|---|---|---|
| 1 | `RecipeDef` | `researchPrerequisite == R` 或 `researchPrerequisites ∋ R` | 各 `products[].thingDef` |
| 2 | `ThingDef` | `researchPrerequisites ∋ R` | 自身 |
| 3 | `ThingDef.plant` | `sowResearchPrerequisites ∋ R` | 自身 |
| 4 | `TerrainDef` | `researchPrerequisites ∋ R` | 自身 |
| 5 | `RecipeDef` | `IsSurgery && researchPrerequisites ∋ R` | 配方本身 |
| 6 | `PsychicRitualDef` | `researchPrerequisite == R` | 自身 |

该列是 `Display`：不落盘（`TweakData.Serialization.cs` 按 `DataType != Field` 过滤）、不参与 `Apply`、不参与 `EqualsData` ⇒ 旧档零迁移。

**缓存失效三条路径**：

| 缓存 | 谁清 |
|---|---|
| `ResearchUnlocks.cache` | ① `Capture`/`Commit` ② `UpdateFilters` 开头兜底 ③ `Init` 末尾 |
| `cachedUnlockedDefs` / `cachedHyperlinks` / `cachedDescription`（原版 private，永不失效） | ① `Commit` 只清"该 def 前后槽位上的项目" ② `Init` 末尾全清 |

- **主路径 `Capture`/`Commit`**：`ApplyValue` / `ApplyClear` / `ApplyAuthoritative` 三个落点在 `Apply` 前后各读一次前置槽位，取并集只清这几个项目（整行/完全重置不走 `ApplyValue`；"被移出前置"的项目也得清）。
- **`UpdateFilters` 兜底**：覆盖第三方 mod 运行时改前置、跨存档残留。
- **`Init` 末尾全清**：加载期 `CompProperties_Techprint.ResolveReferences` 已按 XML 原值填过一份，而我们的写入在 `StaticConstructorOnStartup` 之后。

**已知边界**：

- 图纸物品由 `ThingDefGenerator_Techprints.ImpliedTechprintDefs` 在 `GenerateImpliedDefs_PreResolve` 生成，早于所有写入：`techprintCount` 从 0 改成正数会被拒绝（运行期不补生成物品，且 `Techprint` 为 null 会让 `GetTip()` NRE）；`techprintMarketValue` 会同步到已生成图纸；图纸 `label` / `description` 生成时拼死。
- 研究台 `cachedUnlockedDefsGroupedByPrerequisites` 只在 `PreOpen` 清，后台开着研究台时改前置要重开页签。
- `researchViewX/Y` 只在加载期拷进 `x/y`，改坐标需重开游戏（y 被 clamp 到 6.5，重叠会自动摊开）。
- 不覆盖 `DesignationCategoryDef.researchPrerequisites` 与 `TerrainTemplateDef`（不在原版 `UnlockedDefs` 里）。
- 尚无"增 / 删解锁"写入；若要做：载体 ≠ 产物（一个物品 3 个配方要清 3 个），减法要清所有槽位（列表 + 单值 + 植物播种），加法只写列表槽位（`researchPrerequisite` 与 `researchPrerequisites` 是 AND 语义）。

### 6.3 列过滤（视图过滤）

表头右键 → 「过滤…」打开 `ColumnFilterWindow`，条件按列的**字段类型**给出：

| 判据（按此顺序判定，先命中先用） | `filterKind` | 比较 | 取值来源 |
| --- | --- | --- | --- |
| 触发列（`IsTriggerStyle`：`Link` / 四种 Links / `UnlockList` / Fortified 三列） | `None` | 右键菜单里没有过滤项 | — |
| 任何列表样式（`isListStyle`） | `ListCount` | 项数比较 | `IList.Count`（null 视为 0） |
| `Float` / `Int`（Float / Prec / Int 样式） | `Number` | 大于 / 小于 / 等于 / 范围 | `GetRawValue`，不受"显示默认值 / 结果值"影响；`Prec` 按屏幕单位（百分数）收，比较前 ×0.01 |
| `String`（含走 `OptionsList` 的 Enum） | `Text` | 原版 `QuickSearchFilter.Matches` | `GetString` |
| 其余（Def 引用 / `ThingFilter` / 曲线 / 无选项源的枚举） | `None` | 没有过滤项 | — |

> 顺序是有意义的：`UnlockList` 同时是列表样式与触发列，先判触发 → 它没有"项数"过滤。

端点由 `ColumnFilter.inclusive` 一个开关管（默认含边界）：`≥` / `≤` / 闭区间 `[a, b]`，严格则 `>` / `<` / `(a, b)`；「等于」不受影响。摘要按此口径写：`≥ 5`、`[5 ~ 10]`、`(20% ~ 50%)`、`项数 [2 ~ 5]`。区间不做"留空即无界"（单边用大于/小于），上下限填反在「确定」时自动换。

> 窗口 UI：模式是分段控件 —— 选中项用 `Widgets.DrawOptionBackground(rect, true)` + 粗体字，点击走 `ButtonInvisible`（`ButtonText` 会把刚画的底纹整个盖掉，这也是之前只能靠文字变黄区分的原因）。选中态的粗边画在 `ExpandedBy(3)` 上，也就是按钮**外面**还有 3px：行的上边与右边要留出 `OptionHalo` 那么多空位，否则贴边的按钮会被窗口裁掉（表现为"选中框少一条边"）；左边不用留，按钮左边是"条件"两个字。

四条不变式：

- **文本判据只能用 `QuickSearchFilter`**：自己写 `Contains` 会多出"大小写 / 首尾空格"两种语义差异，顶栏搜索与列过滤就不再是同一件事。
- **数字判据不走 `GetNumericValue`**：它对"当前选中列"有 `showAbstract` / `showChanged` 两条旁路，混进过滤会让同一份数据在点了列头后筛出不同的行。派生列先 `EnsureDisplayValues()`（O(1) 早退）。
- **条件属于视图，不落盘**：`columnFilters` 是 `Dictionary<SettingType, Dictionary<列 id, ColumnFilter>>`，列 id 跨页签会重名，必须按页签分组。
- **条件建好后不再改**：改动 = 建新的，因此导航快照只做浅拷贝存引用。

接缝：`UpdateFilters` 里所有生效条件取"与"，排在 Mod 筛选之后、搜索之前；固定项（`pinnedIds`）绕过过滤；批量修改 / 重置只作用于 `filteredData`；`JumpToData` 清空**当前页签**的过滤（`NavState` 存了旧条件，后退能恢复）；绘制上表头橙色底 + 数据格淡底 + tooltip 追加「当前过滤：…」。过滤窗口里文本清空再确定等同取消。

新增一种过滤方式：`ColumnFilterKind` 加成员 + `StatColumnConfig.ResolveFilterKind`（private）给它来源，再在 `ColumnFilter.Matches` / `Describe` / `ColumnFilterWindow.DrawValueRows` 各补一个分支。`filterKind == None` 的列自动没有过滤项。

### 6.4 种族页的战斗力三列

`RaceData` 上也有 `attackPower` / `defensePower` / `overallPower`（列标题与兵种页共用），但来源不同：race 自身只有身体与基础数值，装备 / 技能 / 预算都挂在 pawnkind 上，所以这三列是**该种族名下所有兵种的平均值**（`CombatPower.FillRace`）。

- Humanlike 下整列不出现（`Available = nameof(AvailableIfNotHumanlike)`）：一个 race 挂上百个兵种，平均既不代表新兵也不代表甲士。列级不适用用 `Available`，不要用 `Hidden`。
- 取**算术平均**而非最高值（非人形种族多为一对一，多出的多是 Anomaly 蹒跚者变体）。
- 刻度与缓存全部复用（`Scale`、`CombatGear.CachedDefense` 按 `(种族, 画像)` 命中）；`CombatPower.Invalidate` 同时置脏 `PawnKind` 与 `Race`；`CombatPower.KindsOf` 建的 race → pawnkind 索引不失效（兵种行的 `Race` 是只读链接）。

## 7. 构建与发版

```powershell
cd Source
dotnet build          # 等价于 dotnet build BalanceTweak.slnx，产物直接落 1.7/Assemblies/
dotnet test           # 测试工程需要 Debug（InternalsVisibleTo 只在 Debug 授权）
dotnet run --project CheckLoc
```

- Debug / Release 产物等价：`OutputPath` 与配置无关，`<Optimize>true</Optimize>` 无条件生效、自检靠 `-p:SelfCheck` 单独开 ⇒ **构建一次即可发布**，不必补 `-c Release`。
- 跑启动期自检（SC1–SC10，会往 `Config` 写 `BalanceTweak_Snapshot_*.txt`）：`dotnet build -p:SelfCheck=true`
- 但**不要对解决方案用 `-c Release`**：`InternalsVisibleTo` 只在 Debug 授权（`AssemblyInfo.cs` 里 `#if DEBUG`），`BalanceTweak.Tests` 会编不过。要出 Release 就点名主工程（下面 1.6 的命令即是）。

### 1.6 是 LTS，默认不编译

`1.6/Assemblies/BalanceTweak.dll` 是发给玩家的冻结产物，编一次即全体 1.6 玩家换版。`TargetFrameworks` 默认只有 `net10.0`，net48 由开关 `IncludeNet48` 按需拉起（csproj 里所有 net48 段都带 `TargetFramework == 'net48'` 条件，关闭时不参与求值）。

```powershell
# 只编 1.6（产物落 1.6/Assemblies/）
dotnet build BalanceTweak/BalanceTweak.csproj -c Release -p:IncludeNet48=true -f net48

# 1.6 + 1.7 一起出
dotnet build BalanceTweak/BalanceTweak.csproj -c Release -p:IncludeNet48=true
```

源码用到 `init` / `required` / 切片 / `HashCode.Combine`，net48 缺的 BCL 类型由 `PolySharp`（源生成器，`PrivateAssets=all`）补齐；编译通过只说明 API 面一致，**行为差异仍需在 1.6 实机验证**。

发版前核对 1.6 产物：`TargetFramework` 应为 `.NETFramework,v4.8`，只引用 `mscorlib` / `Assembly-CSharp` / `0Harmony`，**不得**出现 1.7 专属的 `UnityStubs` / `Waylith`。

### 目录结构：版本目录只放 Assemblies

```
/Defs          ← 底部主按钮（MainButtonDef）
/Languages     ← Keyed + DefInjected（中/英）
/1.6/Assemblies/BalanceTweak.dll   ← net48，LTS 稳定版产物
/1.7/Assemblies/BalanceTweak.dll   ← net10.0
```

`LoadFolders.xml` 里 v1.6 与 v1.7 都加载根目录 `/`，所以 **Defs 与 Languages 放根目录**，两版共用，`1.6/` `1.7/` 只放 Assemblies。别再往版本目录放 Defs / Languages 副本 —— 两份会漂移。某个 Def 能放根目录的前提是它引用的类型（如 `workerClass`）在两个 dll 里都存在。
