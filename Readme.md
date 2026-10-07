# Rimworld BalanceTweak Mod 开发流程

## 硬约束（改代码前必读）

存档里每条修改的身份是三元组 `(数据类 FullName, defName, SettingType 枚举名)`，三者任一变动都会让旧数据失配（完整分析见 `ThingDefCoverage_Design.md` §10）：

| 禁止 | 原因 |
|------|------|
| 重命名 / 改 namespace / 删除仍在用的 `XxxData` 类 | 存档每条都写 `Class="BalanceTweak.XxxData"`；解析失败整条数据变 null（`Init` 里已加 null 防御，但数据仍会丢） |
| 重命名 / 删除 `SettingType` 枚举成员 | 存档按**名称**写入，解析失败退化为 `None` 并失效。**重排序号是安全的** |
| 重命名 `[TweakField]` 字段 | 字段名 = XML 元素名，改名即丢该字段的修改（新增字段安全） |
| 直接删除还在使用的 `[TweakFor]` 规则 | 先确认没有已存数据落在该页签；确需删除时保留一个继承它的空壳子类 |

安全的改动：末尾追加枚举成员、新增 `[TweakField]` 字段、调整 `MatchMethod` 判定与 `Priority`（分类漂移会被自动检测并重新归类）、修改 `Apply` 逻辑。

## 新增可编辑 Def 类型的工作流程

### 1. 查 RimWorld 源码
可使用 `decompiler` 工具（如果有）查看 C# 定义。
可用 `rimworld-source` 工具（如果有）查看 C# 定义以及游戏中实际数据。
可用 `rimworld-data` 工具（如果有）查看游戏中实际数据。

### 2. 参考已有的 Data 文件
在 `Source/BalanceTweak/Data/` 下找类似模板。
- **关键模式**：类继承 `TweakData<T>`、`[TweakField()]` 标记字段、实现 `SetDef/Apply/GetPropType/TypeStrings`，并加上声明式注册特性 `[TweakFor]`（见第 4 步）。
- **Apply 中的 Stat 写入**：对于 `StatDef` 标记的字段，子类 `Apply()` 中**调用基类的 `ApplyDefStats(def)` 一行即可**。它会按 `IsEquippedStat` / `IsStuffFactorStat` / `IsStuffOffsetStat` 自动选择正确的写入位置，并把 `statBases` 等 `List<StatModifier>` 字段与 def 双向同步。
- **子项**：若该类型自带子项（阶段、招式、程度……），实现 `ISubItemHost`（见第 5 步）。
- **分类**：不再需要改 `TweakDatabase`。分类、Apply 阶段的 Def 解析、`GetData(Def)` 的自动探测全部由 `[TweakFor]` 规则表驱动。

### 3. 编写 Data 文件
在 `Source/BalanceTweak/Data/` 下创建 `{Name}Data.cs`。参考现有实现（如 `WeatherData` 或 `FoodData`）作为模板。

| 字段来源 | 读写方式 | 示例 |
|---------|---------|------|
| **StatDef 字段** | `GetStatValueAbstract` / `SetStatBaseValue`，`[TweakField(StatDef = "字段名")]` | Nutrition |
| **直接字段** | 直接读写 `def.字段名` | `stackLimit` |
| **子对象字段** | `def.ingestible.字段名`，SetDef/Apply 中判空 | `baseIngestTicks` |
| **Enum 字段** | `[TweakField(Style = ColumnStyle.Enum, EnumType = typeof(XxxEnum))]` | FoodPreferability |
| **Def 引用字段** | `[TweakField(Style = ColumnStyle.DefSelector)]`，`??=` 从 def 读取 | `spawnThingOnRemoved` |
| **`List<T>`** | `[TweakField(Style = ColumnStyle.DefList)]`，`??= def.list` | `makeImmuneTo`、`enablesNeeds` |


所有字段用 `??=` 从 def 读取默认值。

### 3b. 处理 Comp（CompProperties）

- **Bool 开关（添加/移除 Comp）**：参考 `ApparelData.isCompQuality`。`false` 移除、`true` 添加（先检查防重复）。
- **修改已有 Comp 属性**：参考 `BuildingData`，在 `SetDef/Apply` 中遍历 `def.comps` 用 `is` 匹配子类型后读写字段。
- **Comp 存在性检查**：`def.HasComp<T>()` / `def.HasAssignableCompFrom<T>()` / `def.GetCompProperties<T>()` / `def.comps.Any(...)`。
- **添加 Comp**：`new CompProperties(typeof(CompX))`，添加前务必检查是否已存在。

### 4. 声明分类规则（唯一的注册步骤）

在 Data 类上加 `[TweakFor]`，声明它负责哪一类 Def、显示在哪个页签、以什么优先级参与分类：

```csharp
[TweakFor(typeof(WeatherDef), SettingType.Weather)]
class WeatherData : TweakData<WeatherData> { /* ... */ }
```

**四种声明方式：**

- **专用 Def 类型**：`DefType` 直接写 Def 类型即可，该类全部 Def 都归它管。
- **ThingDef 子类别**：`DefType = typeof(ThingDef)` + `MatchMethod` 指定判定方法，`Priority` 决定抢占顺序（大者先匹配，等价于原 `else if` 链的先后）。判定方法可以写在自己的 Data 类里，也可以复用 `DataUtility.MatchXxx`：
  ```csharp
  [TweakFor(typeof(ThingDef), SettingType.Weapon, Priority = 200, MatchMethod = nameof(DataUtility.MatchWeapon))]
  ```
- **跨 Mod 反射类型**：用 `TypeName` 指定类型全名（反射获取，拿不到就跳过），`MayRequire` 指定所需 Mod：
  ```csharp
  [TweakFor(typeof(RecipeDef), SettingType.Recipe, TypeName = "PipeSystem.ProcessDef", MayRequire = TweakDatabase.VEF)]
  ```
- **兜底类型**：`IsFallback = true`，只在同 Def 类型下没有任何规则命中时生效（如 `MiscThingData`）。
- **并列挂载**：`Exclusive = false` 允许一个 Def 同时挂多个 Data；默认 `true`，与原 if/else-if 链的"命中即停"语义一致。

`TweakDatabase` **不需要任何改动**。注册失败（Match 方法找不到、缺无参构造等）会在启动日志里以 `TweakRegistry 注册失败：…` 报出。

### 5. 注册子项（可选）

若该类型会产出子项（招式 / 阶段 / 特性程度……），实现 `ISubItemHost`：

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

实现方负责设置 `index`、调用 `SetParentTweak`、跳过空条目。`TweakDatabase` 会自动收集，不必在初始化流程里加分支。

### 6. 添加本地化翻译

在 `Languages/English/Keyed/` 和 `Languages/ChineseSimplified/Keyed/` 目录下，根据新增 Def 类型对应的功能分类，在相应的 XML 文件中添加 key（格式 `<MST.名称>值</MST.名称>`）。

**文件映射规则**：每个 SettingType 对应一个 XML 文件，新增枚举值时需创建对应的新文件。

> **共享键不要重复定义**：`defLabel` / `statBases` / `pathCost` / `stackLimit` / `destroyable` / `Misc`
> 这类被多个类型共用的键，直接复用已有定义（多数在 `Core.xml`），**不要在新类型的 XML 里再写一遍**。
> `CheckLoc` 把 `Core.xml` 当作跨文件重复键的规范位置，重复定义会产生告警。

| 需要本地化的内容 | Key 格式 | 说明 |
|----------------|---------|------|
| SettingType 标签页标题 | `MST.{枚举值}` | 新增的 SettingType 枚举值 |
| [TweakField] 字段列标题 | `MST.{字段名}` |
| 私有 enum 分类名 | `MST.{枚举值}` | Data 中用于分类的 enum |
| 字段 tooltip 说明 | `MST.{字段名}Comment` | 含义不直观或带风险的字段添加 |

**SettingType 与 XML 文件对照表：**

| SettingType | XML 文件名 |
|------------|-----------|
| `Core`（通用 UI） | `Core.xml` |
| `Race` | `Race.xml` |
| `Apparel` | `Apparel.xml` |
| `Weapon` | `Weapon.xml` |
| `MeleeTool` | `MeleeTool.xml` |
| `Ability` | `Ability.xml` |
| `Stuff` | `Stuff.xml` |
| `Building` | `Building.xml` |
| `Projectile` | `Projectile.xml` |
| `Thought` | `Thought.xml` |
| `ThoughtStage` | `Thought.xml`（合并于 Thought 文件） |
| `PawnKind` | `PawnKind.xml` |
| `Gene` | `Gene.xml` |
| `Meme` | `Meme.xml` |
| `Incident` | `Incident.xml` |
| `Research` | `Research.xml` |
| `Weather` | `Weather.xml` |
| `Faction` | `Faction.xml` |
| `Food` | `Food.xml` |
| `Hediff` | `Hediff.xml` |
| `HediffStage` | `Hediff.xml`（合并于 Hediff 文件） |
| `Damage` | `Damage.xml` |
| `BodyPart` | `BodyPart.xml` |
| `BodyDef` | `BodyDef.xml` |

> 新增 SettingType 枚举值时，需同时创建对应的 XML 文件，并在 `Core.xml` 中添加通用 UI 相关的 key。

## TweakField 属性说明

| 参数 | 用途 | 示例 |
|------|------|------|
| `Style` | 列显示样式 | `ColumnStyle.Float / String / Enum / Int / Bool / Prec / Link / RaceLinks / BodyLinks / StatModList / CapModList / DamageModList / DefList / DefSelector / StringList / IntList` |
| `EnumType` | Enum 类型（下拉菜单） | `typeof(FoodPreferability)` |
| `StatDef` | 关联 StatDef 名称 | `"Nutrition"` |
| `MayRequire` | 需要某 Mod 才显示 | `"ceteam.combatextended"` |
| `IsEquippedStat` | 是否为装备 stat | `true` |
| `IsStuffFactorStat` | 是否为材料倍率 stat | `true` |
| `DataType` | `Field`（可编辑）或 `Display`（只读） | `ColumnDataType.Display` |
| `Available` | 显示条件方法名 | `"AvailableMethodName"` |
| `GetAbstract` | 获取抽象值方法名 | `"GetAbstractMethodName"` |
| `OptionsList` | 静态列表字段名（动态选项） | `"ResearchTabDefs"` |

### ColumnStyle 与字段类型对照

| ColumnStyle | 适用的 C# 类型 | 说明 |
|------------|---------------|------|
| `Float`（默认） | `float?` | 浮点数输入 |
| `Int` | `int?` | 整数输入 |
| `Bool` | `bool?` | 开关切换 |
| `String` | `string?` | 文本输入 |
| `Enum` | `Nullable<Enum>` | 下拉菜单，需配合 `EnumType` |
| `Flags` | `Nullable<Enum>`（带 `[Flags]`） | 标志位多选编辑器 |
| `Prec` | `float?` | 高精度浮点（值/100） |
| `Curve` | `SimpleCurve` | 曲线编辑器 |
| `Link` | `TweakID?` | 跳转到关联子项目链接 |
| `RaceLinks` | `TweakID?` | 只读关联入口：点击列出反向关联的种族数据（见 [附2](#附2-关联列racelinks--bodylinks)） |
| `BodyLinks` | `TweakID?` | 只读关联入口：点击列出反向关联的身体数据（见 [附2](#附2-关联列racelinks--bodylinks)） |
| `DefSelector` | `T?` where T : Def | Def 选择器（弹出搜索窗口） |
| `StatModList` | `List<StatModifier>?` | StatModifier 列表编辑器 |
| `CapModList` | `List<PawnCapacityModifier>?` | PawnCapacityModifier 列表编辑器 |
| `DamageModList` | `List<DamageFactor>?` | DamageFactor 列表编辑器 |
| `DefList` | `List<T>?` where T : Def | Def 列表编辑器（添加/删除/替换） |
| `HediffGiverList` | `List<HediffGiver>?` | HediffGiver 列表编辑器 |
| `MentalStateGiverList` | `List<MentalStateGiver>?` | MentalStateGiver 列表编辑器 |
| `SkillGainList` | `List<SkillGain>?` | SkillGain 列表编辑器 |
| `SkillReqList` | `List<SkillRequirement>?` | SkillRequirement 列表编辑器 |
| `AptitudeList` | `List<Aptitude>?` | Aptitude 列表编辑器 |
| `GeneticTraitList` | `List<GeneticTraitData>?` | GeneticTraitData 列表编辑器 |
| `StringList` | `List<string>?` | 字符串列表编辑器（添加/编辑/删除） |
| `IntList` | `List<int>?` | 整数列表编辑器（添加/编辑/删除） |
| `ThingDefCountList` | `List<ThingDefCountClass>?` | ThingDefCountClass 列表编辑器 |
| `IngredientList` | `List<IngredientCount>?` | IngredientCount 列表编辑器 |
| `IngredientFilter` | `ThingFilter?` | ThingFilter 过滤器编辑器 |

---

## 7. 为现有 Data 类添加新字段

适用场景：已有的 Data 类（如 `HediffStageData`、`ThoughtStageData`）需要新增可编辑字段。

### 7.1 确定字段类型与 Style

在 RimWorld 源码中找到目标字段类型后，按 [ColumnStyle 与字段类型对照](#columnstyle-与字段类型对照) 选择对应 `ColumnStyle`。

### 7.2 三处修改模式

无论哪种 Data 类，添加字段都涉及 **3 处修改**：

```csharp
// 1. 字段声明（文件顶部）
[TweakField(Style = ColumnStyle.DefList)]
public List<HediffDef>? makeImmuneTo = null;

// 2. 初始化（SetDef / SetParentTweak 中从 def 读取默认值）
makeImmuneTo ??= stage.makeImmuneTo;

// 3. 写入（Apply 中写回 def）
if (makeImmuneTo != null) { stage.makeImmuneTo = makeImmuneTo; }
```

**初始化规则：**
- 值类型（`float?`/`int?`/`bool?`）：`field ??= def.fieldName;`
- 引用类型（`string?`、Def 引用）：`field ??= def.fieldName;`
- 列表类型（`List<T>?`）：`field ??= def.fieldName;`

**写入规则：**
- 值类型：`if (field.HasValue) { def.fieldName = field.Value; }`
- 引用类型：`if (field != null) { def.fieldName = field; }`
- 列表类型：`if (field != null) { def.fieldName = field; }`

> **对于 `[TweakField(StatDef = "...")]` 标记的 StatDef 字段**，写入步骤**不要手写 `if` 块**，而是在 `Apply()` 中**直接调用基类方法**一行完成：
> - `ApplyThingDefStats(def)` — ThingDef 及其子类（Race、Apparel、Weapon、Building、Food、Stuff 等）
> - `ApplyAbilityDefStats(def)` — AbilityDef
> - `ApplyTerrainDefStats(def)` — TerrainDef
> 
> 基类方法会自动遍历所有 `[TweakField(StatDef)]` 字段，根据 `IsEquippedStat` / `IsStuffFactorStat` 属性选择正确的 `TrySetStat*` 方法写入。参考现有实现如 `RaceData.Apply()` 或 `ApparelData.Apply()`。
>
> **对于 `[TweakField(Style = ColumnStyle.StatModList)]` 标记的 `List<StatModifier>` 字段**（如 `statBases`、`equippedStatOffsets`），写入步骤也不需要手写 `if` 块。`ApplyThingDefStats(def)` 已通过 `StatDef` 写入单个 Stat 值到 def 的对应列表，之后在 `Apply()` 中**调用 `SyncStatModListsFromDef(def)`** 将 def 上的最终列表状态同步回 Data 字段，保证 UI 与 def 双向一致：
> ```csharp
> public override void Apply()
> {
>     if (this.def is not ThingDef def) return;
>     ApplyThingDefStats(def);      // 写入单个 StatDef 字段到 def.statBases / def.equippedStatOffsets
>     SyncStatModListsFromDef(def); // 将 def.statBases / def.equippedStatOffsets 同步回 List<StatModifier> 字段
> }
> ```
> 参考实现：`StuffData.Apply()`。

### 7.3 子项目（Sub-item）Data 特殊模式

`HediffStageData`、`ThoughtStageData` 等子项目 Data 不继承 `TweakData<T>.SetDef()`，而是使用 **`SetParentTweak`** 模式：

| 特点 | 说明 |
|------|------|
| **私有字段** | 持有子对象引用（如 `HediffStage? stage`），通过它读写 def 数据 |
| **`SetParentTweak`** | 替代 `SetDef`，由父 Data（如 HediffData）回调，接收父 TweakData 引用 |
| **`parentTweakId`** | 记录父项目 ID，用于 `GetPropType()` 继承父级分类 |
| **`index`** | 存储子项目在父列表中的索引 |
| **身份校验** | 通过 `untranslatedLabel` 与保存的 `stageLabel` 比对，防止 HediffDef 重命名后错位 |
| **空阶段处理** | 空 `<li></li>` 返回 null，跳过初始化 |

添加字段时，**不是**通过 `def.stages[i].fieldName` 访问，而是通过 `stage.fieldName`（已缓存的子对象引用）。

### 7.4 列表字段序列化

各列表类型的序列化在 `TweakData.ExposeField()` 中自动完成，声明字段即可：

| 列表类型 | 序列化格式 | 存储示例 |
|---------|-----------|---------|
| `List<StatModifier>?` | `"defName\|value"` 字符串列表 | `"MoveSpeed\|0.2"` |
| `List<PawnCapacityModifier>?` | `"defName\|offset\|setMax\|postFactor\|statFactor\|evalStat"` 字符串列表 | `"Moving\|0.1\|-1\|1\|\|"` |
| `List<DamageFactor>?` | `"damageDefName\|factor"` 字符串列表 | `"Bullet\|0.5"` |
| `List<T>?` where T : Def | `defName` 字符串列表 | `"Gunshot"`, `"Flu"` |
| `List<string>?` | 纯字符串列表 | `"tag1"`, `"tag2"` |
| `List<int>?` | 整数列表 | `"1"`, `"2"`, `"3"` |
| `List<ThingDefCountClass>?` | `"thingDefName\|stuffDefName\|count"` 字符串列表 | `"Steel\|\|5"`, `"WoodLog\|Wood\|3"` |

新增列表类型时，需在 `StatColumnConfig.IsListStyle()` 静态方法中添加对应枚举值，以便 `GetString()` 等方法统一处理。

> **新增列表/复合字段类型（ColumnStyle）的接线清单**（参考 `TraitReqList` / `SkillReqList`）：
>
> 1. **`StatColumnConfig.cs`**：`ColumnStyle` 枚举加成员；加入 `ValidStyleMap`（FieldType.List 数组）与 `IsListStyle()`；在 `GetCopyData()` 的序列化 switch、`TryPasteData()` 的剪贴板解析、`GetString()` 的单元格摘要 case 中各加一个分支。
> 2. **`SerializationHelper.cs`**：新增 `{Type}ToItemString` / `Parse{Type}` / `Serialize{Type}List` / `Deserialize{Type}List`（管道 `|` 分隔格式；Def 解析不到时用 `LogMissingDef` 并返回 null）。
> 3. **`TweakData.Serialization.cs`**：`ExposeField()` 加类型分发分支 + `ExposeXxxList()`（存档读写）。
> 4. **`TweakData.Resolution.cs`**：加类型分发分支 + `ResolveXxxList()`（任一条解析失败返回 `(null, false)`，整条数据放弃）。
> 5. **编辑器窗口**：新建 `GUI/Editor/{Style}EditorWindow`（继承 `ListEditorWindow<T>`），并在 `BalanceTweakGUI.cs` 的窗口工厂表注册。
> 6. **本地化**：`MST.{Style}`（枚举标签，放 `Core.xml`）；字段标题 `MST.{字段名}(+Comment)`；窗口内按钮键（如 `MST.AddXxx`，可复用已有的 `MST.SelectXxx`）。

### 7.5 添加本地化

任何字段需添加列标题翻译。对于含义不直观的字段，建议同时添加 `{FieldName}Comment` tooltip（StatDef由游戏提供Comment，因此无需说明）：

```xml
<MST.makeImmuneTo>免疫</MST.makeImmuneTo>
<MST.makeImmuneToComment>此阶段使角色免疫的疾病/健康状态列表。</MST.makeImmuneToComment>
```

`Comment` 后缀的 key 会自动作为 tooltip 显示在列标题上。

### 7.6 构建验证

修改完成后执行 `dotnet build` 验证编译通过：

```powershell
cd Source
dotnet build BalanceTweak.slnx --configuration Debug
```

---

## 附1. ColumnStyle 编辑窗口与用法

| ColumnStyle | 编辑方式 | 窗口类 | C# 类型 |
|------------|---------|--------|-------------|
| `Float` | 内联文本框 | — | `float?` |
| `Int` | 内联文本框 | — | `int?` |
| `String` | 弹出窗口 | `StringEditorWindow` | `string?` |
| `Prec` | 内联文本框 + % | — | `float?`（值/100） |
| `Bool` | 内联复选框 | — | `bool?` |
| `Enum` | FloatMenu 下拉菜单 | — | `Nullable<Enum>`，需 `EnumType` 或 `OptionsList` |
| `Link` | 内联跳转链接 | — | `TweakID?`，`DataType=Display` |
| `RaceLinks` | 弹出窗口 | `TweakLinksWindow` | `TweakID?`，只读触发列，`DataType=Display` |
| `BodyLinks` | 弹出窗口 | `TweakLinksWindow` | `TweakID?`，只读触发列，`DataType=Display` |
| `Curve` | 弹出窗口 | `CurveEditorWindow` | `SimpleCurve?` |
| `Flags` | 弹出窗口 | `FlagsEditorWindow` | `Nullable<Enum>`（带 `[Flags]`） |
| `StatModList` | 弹出窗口 | `StatModListEditorWindow` | `List<StatModifier>?` |
| `CapModList` | 弹出窗口 | `CapModListEditorWindow` | `List<PawnCapacityModifier>?` |
| `DamageModList` | 弹出窗口 | `DamageModListEditorWindow` | `List<DamageFactor>?` |
| `DefList` | 弹出窗口 | `DefListEditorWindow` | `List<T>? where T : Def` |
| `HediffGiverList` | 弹出窗口 | `HediffGiverListEditorWindow` | `List<HediffGiver>?` |
| `MentalStateGiverList` | 弹出窗口 | `MentalStateGiverListEditorWindow` | `List<MentalStateGiver>?` |
| `SkillGainList` | 弹出窗口 | `SkillGainListEditorWindow` | `List<SkillGain>?` |
| `SkillReqList` | 弹出窗口 | `SkillReqListEditorWindow` | `List<SkillRequirement>?` |
| `AptitudeList` | 弹出窗口 | `AptitudeListEditorWindow` | `List<Aptitude>?` |
| `GeneticTraitList` | 弹出窗口 | `GeneticTraitListEditorWindow` | `List<GeneticTraitData>?` |
| `StringList` | 弹出窗口 | `StringListEditorWindow` | `List<string>?` |
| `IntList` | 弹出窗口 | `IntListEditorWindow` | `List<int>?` |
| `ThingDefCountList` | 弹出窗口 | `ThingDefCountListEditorWindow` | `List<ThingDefCountClass>?` |
| `IngredientList` | 弹出窗口 | `IngredientListEditorWindow` | `List<IngredientCount>?` |
| `IngredientFilter` | 弹出窗口 | `IngredientFilterEditorWindow` | `ThingFilter?` |
| `ProcessIngredientList` | 弹出窗口 | `ProcessIngredientListEditorWindow` | `List<ProcessIngredientItem>?` |
| `ProcessResultList` | 弹出窗口 | `ProcessResultListEditorWindow` | `List<ProcessResultItem>?` |
| `DefSelector` | 弹出窗口 | `DefSelectionWindow` | `T? where T : Def` |