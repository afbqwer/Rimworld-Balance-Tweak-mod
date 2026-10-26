# Rimworld BalanceTweak Mod 开发流程

## 硬约束（改代码前必读）

存档里每条修改的身份是三元组 `(数据类 FullName, defName, SettingType 枚举名)`，三者任一变动都会让旧数据失配（完整分析见 `ThingDefCoverage_Design.md` §10）：

| 禁止 | 原因 |
|------|------|
| 重命名 / 改 namespace / 删除仍在用的 `XxxData` 类 | 存档每条都写 `Class="BalanceTweak.XxxData"`；解析失败整条数据变 null（`Init` 里已加 null 防御，但数据仍会丢） |
| 重命名 / 删除 `SettingType` 枚举成员 | 存档按**名称**写入，解析失败退化为 `None` 并失效。**重排序号是安全的** |
| 重命名 `[TweakField]` 字段 | 字段名 = XML 元素名，改名即丢该字段的修改（新增字段安全） |
| 直接删除还在使用的 `[TweakFor]` 规则 | 先确认没有已存数据落在该页签；确需删除时保留一个继承它的空壳子类 |
| 用「值 == null / 值为空」反推"用户清空了该字段" | L0 三态只认两个**显式**集合（`TweakData.StateOf`）：`_modifiedFieldNames`（Set）与 `_clearedFieldNames`（Cleared）。清空**只能**由编辑器的清空动作（`MarkFieldCleared`）写入；判定不清空/不写入一律走 `ApplyV/ApplyN/ApplyL/ApplyD/ApplyS/ApplyF` 门控 |
| 绕过 `MarkFieldCleared` 直接改 `_clearedFieldNames`，或让同一字段同时出现在两个集合里 | 两集合互斥是 Set/Cleared 的唯一判据；破坏互斥会让 `StateOf` 二义、`IsFieldModified` 回填守卫与 `Apply` 门控错乱 |
| 给 `List<>` / `ThingFilter` 字段加 `??=` 回填点却不带 `!IsFieldModified(...)` 守卫 | Cleared 字段读档后会被 Def 回填"复活"，清空意图重启即失效。运行期自检 SC2 会报 `[BT/SELFCHECK] 复活:` |
| 在代码里**静态引用可选 Mod 的类型**（`typeof` / `is` / 泛型参数 / 把该类型变量捕获进 lambda） | C# 会把 lambda 捕获的变量编成闭包类的**公共字段**；未加载该 Mod 的玩家在启动期 `StaticConstructorOnStartupUtility` 对全部类型调 `GetFields()` → `TypeLoadException`（`Could not load type of field '...<>c__DisplayClass...'`）。一律按全名反射：`DataUtility.IsTypeOrSubclassOfName / FindMember / GetByName / SetByName / FindModExtension`，闭包里只许出现 `object`/`MemberInfo` |
| 给可选 Mod 的 dll 加 csproj `<Reference>` | 有引用，编译器才**能**编出跨程序集 typeref；**不引用** = 编译期杜绝（可选 Mod 的成员只能按字符串名字访问）。`PipeSystem` / `VEF` / `VanillaPsycastsExpanded` 均已移除引用 |

安全的改动：末尾追加枚举成员、新增 `[TweakField]` 字段、调整 `MatchMethod` 判定与 `Priority`（分类漂移会被自动检测并重新归类）、修改 `Apply` 逻辑。

## Apply 门控与可选 Mod 反射 helper 索引

### A. Apply 系列（`TweakData.Typed.cs`，访问级别 `protected internal`）

三态门控的**唯一写入入口**：按 `StateOf(f)` 决定写不写、怎么写，**绝不用 `!= null` / `HasValue` 短路**（硬约束表第 5 行）。
`Set` → 写字段当前值；`Unset` → 什么都不做；`Cleared` → 按下面最后一列处理。

| helper | 目标 | 签名 | `Cleared` 的动作 |
| ------ | ---- | ---- | ---------------- |
| `ApplyV<TV>` | 非空目标 / 标量（`Action<float>`、`Action<int>`、`Action<FloatRange>`…） | `ApplyV<TV>(string f, Action<TV> set)` | 还原原值（`Revert`） |
| `ApplyN<TV>` `where TV:struct` | 可空目标（`float?` / `int?` / `IntRange?` / `QualityCategory?`…） | `ApplyN<TV>(string f, Action<TV?> set)` | 置 `null` |
| `ApplyL<TV>` | 集合（`List<T>`） | `ApplyL<TV>(string f, Action<List<TV>?> set)` | 置 `null`（清空） |
| `ApplyD<TV>` `where TV:Def` | Def 引用 | `ApplyD<TV>(string f, Action<TV?> set)` | 还原原引用（`Revert`） |
| `ApplyS` | `string` | `ApplyS(string f, Action<string> set)` | 还原原字符串（`Revert`） |
| `ApplyF` | `ThingFilter` | `ApplyF(string f, Action<ThingFilter> set)` | 无 Cleared 分支（空过滤器是有效值） |

- `f` 一律写 `nameof(字段名)`；setter 里直接赋给目标：`ApplyV<float>(nameof(workAmount), v => def.workAmount = v)`。
- **标量选 `ApplyV` 还是 `ApplyN`**：取决于"清空"语义 —— 想要还原原值用 `ApplyV`，想要写成 `null` 用 `ApplyN`。
- `Revert` 原值取自 `TweakDatabase.GetOriginal(id)`；原值缺席时**保持现状 + 每字段告警一次，不抛异常**。
- `StatDef` 字段**不要**用这些 helper，改用 `ApplyDefStats(def)`（见 §7.2）。
- 字符串列不开放清空，故 `ApplyS` 的 Cleared 分支平时不走。

### B. 可选 Mod 反射系列（`DataUtility`，全名反射，严禁静态引用）

未加载可选 Mod 时不得出现跨程序集 typeref（硬约束表最后两行）。跨 Mod 兼容一律按**字符串全名**访问，命名空间 `using` 与 csproj `<Reference>` 都**不要**加。

| helper | 用途 |
| ------ | ---- |
| `IsTypeOrSubclassOfName(Type?, string fullName)` | 类型或其基类是否等于某全名（替代 `is` / `typeof(...) ==` 判断） |
| `FindMember(Type?, string name)` | 沿继承链找 `MemberInfo`（字段或属性，含非公开，`DeclaredOnly`）。返回可空 |
| `GetMember(object?, MemberInfo?)` / `SetMember(object?, MemberInfo?, object?)` | 用已缓存的 `MemberInfo` 读 / 写 |
| `GetByName(object?, string)` / `SetByName(object?, string, object?)` | 一步式（内部 `FindMember`）。**高频访问请先缓存 `MemberInfo`** |
| `AsFloat` / `AsInt` / `AsBool` / `AsIntList(object?)` | 把反射取回的 `object` 安全转成目标类型（失败返回 `null` / `false`） |
| `FindModExtension(Def?, string fullTypeName)` | 按全名找 `DefModExtension` |
| `MayRequire(string? packageId)` | 判断某 Mod 是否加载（`[TweakFor(MayRequire = ...)]` 也用它） |

约定：
- **lambda 只许捕获 `object` / `MemberInfo`**，绝不能把可选 Mod 类型的变量捕获进闭包 —— C# 会把它编成闭包类的公共字段，未装该 Mod 的玩家启动期 `GetFields()` 直接 `TypeLoadException`。
- 引用实现：`Data/AbilityData.cs`（VEF / VanillaPsycastsExpanded）、`Data/ProcessItems/RecipeProcessHelper.cs`（PipeSystem）。

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

> **Def 的 private 字段与懒加载缓存**：目标字段是 private（如 `BiomeDef.wildAnimals`）时，用 `AccessTools.Field(typeof(XxxDef), "字段名")` 缓存 `FieldInfo` 读写（先例见 `SerializationHelper.cs` 的 ThingFilter 内部字段）。
> 若 Def 把派生数据缓存在私有字段里（如 BiomeDef 的 `cachedAnimalCommonalities` / `cachedPlantCommonalities` / `cachedDiseaseCommonalities` / `cachedWildPlants` 等，懒加载且无失效机制），Apply 写完列表后需反射把这些缓存字段置 `null` 强制重建，否则运行中修改不生效。参考 `BiomeData.cs`。
>
> **复合对象子字段的列表**：若字段是复合对象（如 `BiomeDef.fishTypes` 的 `freshwater_Common` 等 List 子字段），在 Data 类上按子字段逐个建 `[TweakField]`；Apply 中任一子字段非空时先 `def.fishTypes ??= new BiomeFishTypes()` 再赋值。DLC 专属内容（Odyssey/Biotech）用 `[TweakField(MayRequire = "ludeon.rimworld.odyssey")]` 门控列显示。

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
| `Biome` | `Biome.xml` |
| `Trader` | `Trader.xml` |
| `Xenotype` | `Xenotype.xml` |
| `ArrivalMode` | `ArrivalMode.xml` |

> 新增 SettingType 枚举值时，需同时创建对应的 XML 文件，并在 `Core.xml` 中添加通用 UI 相关的 key。

## TweakField 属性说明

| 参数 | 用途 | 示例 |
|------|------|------|
| `Style` | 列显示样式 | `ColumnStyle.Float / String / Enum / Int / Bool / Prec / Link / RaceLinks / BodyLinks / StatModList / CapModList / DamageModList / DefList / DefSelector / StringList / IntList / StockGenList` |
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
| `BiomePlantList` | `List<BiomePlantRecord>?` | BiomeDef 野生植物列表编辑器（plant + commonality） |
| `BiomeAnimalList` | `List<BiomeAnimalRecord>?` | BiomeDef 野生动物列表编辑器（animal + commonality） |
| `FishChanceList` | `List<FishChance>?` | 鱼类列表编辑器（fishDef + chance） |
| `XenotypeChanceList` | `List<XenotypeChance>?` | 异种人几率列表编辑器（xenotype + chance） |
| `DiseaseList` | `List<BiomeDiseaseRecord>?` | 疾病列表编辑器（diseaseInc + commonality） |
| `WeatherCommonalityList` | `List<WeatherCommonalityRecord>?` | 天气列表编辑器（weather + commonality） |
| `TerrainThresholdList` | `List<TerrainThreshold>?` | 地表肥沃度区间列表（terrain + min/max） |
| `StockGenList` | `List<StockGenerator>?` | 库存生成器列表编辑器（每行一个生成器，按字段表声明的种类给出输入框/下拉/子列表；类型菜单只列原版类型，非原版条目只读展示类型名）；字段直接持有对象，见 [§7.6](#76-多态列表字段直接持有对象--列表窗口编辑stockgenlist) |
| `FactionCurveList` | `List<FactionCurve>?` | 派系曲线列表编辑器（faction + selectionWeightPerPointsCurve）；曲线值文本为 `[x~y;x~y]`（PocoCodec `Kind.Curve`），faction 解码需 DefDatabase |
| `SkillRangeList` | `List<SkillRange>?` | 技能区间列表编辑器（skill + IntRange）；`SkillRange` 两个字段都是 private，读写走 PocoCodec 字段表 |

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

// 3. 写入（Apply 中写回 def）—— 用三态门控 helper，不要手写 `if`
ApplyL<HediffDef>(nameof(makeImmuneTo), v => stage.makeImmuneTo = v);
```

**初始化规则：**
- 值类型（`float?`/`int?`/`bool?`）：`field ??= def.fieldName;`
- 引用类型（`string?`、Def 引用）：`field ??= def.fieldName;`
- 列表类型（`List<T>?`）：`field ??= def.fieldName;`

**写入规则：** 一律走 **Apply 门控 helper**（见文首《Apply 门控与可选 Mod 反射 helper 索引》），**不要手写 `if (field != null)`** —— 那会绕过 `Set`/`Cleared` 判定（硬约束表第 5 行），把用户的清空意图吃掉。按目标选 helper：
- 标量（非空目标）：`ApplyV<float>(nameof(f), v => def.f = v);`
- 可空目标：`ApplyN<float>(nameof(f), v => def.f = v);`（也可用 `ApplyV`，见索引中的取舍说明）
- 引用 / Def 引用：`ApplyD<SkillDef>(nameof(f), v => def.f = v);`
- 列表：`ApplyL<HediffDef>(nameof(f), v => def.list = v);`
- 字符串：`ApplyS(nameof(f), v => def.f = v);`

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

### 7.4 列表 / 复合字段的接线清单

**存档文本与剪贴板格式只有一处定义**：`TweakData.FieldCodecs.cs` 的字段表（`PocoList<T>(...)`）。
字段表里登记的列表格式恒为 `字段名=值|字段名=值`，顺序即声明顺序；等于"新实例初值"的字段省略；列表值写成 `[a;b]`。
`PocoCodec` 负责编解码、转义、任一条失败即整块拒绝。**不要再写逐类型的手写编码器** ——
元素类型进了字段表，格式就自动有了；`SerializationHelper` 里现存的 `SerializeXxxList` 只剩 bespoke 用途
（`IngredientCount` / `List<int>` / `List<Def>` / 部位树），新增 POCO 列表不必碰它。

判据只有一条：字段 / 元素类型能不能进字段表。先看 [ColumnStyle 与字段类型对照](#columnstyle-与字段类型对照)，
能复用现成样式就别新增 —— `DefList`（`List<T> where T : Def`）、`StringList`、`IntList`、`StatModList` 这几类都已有。

#### A. 元素类型是普通 POCO —— 绝大多数情况（`SkillRange` / `TerrainThreshold` / `SkillRequirement`）

1. **`TweakData.FieldCodecs.cs`**：`Build()` 里加一行 `PocoList<T>("字段1", "字段2", ...)`。
   这一行同时完成三件事：声明字段表（**顺序即文本顺序**）、把 `List<T>` 登记进落盘/读档、让剪贴板与窗口的默认实现可用。
   字段是 `private` 也没问题（按 `FieldInfo` 读写，`SkillRange` 就是先例）；要校验取值范围就写 `PocoList<T>(validate, ...)`。
2. **`StatColumnConfig.cs`**：`ColumnStyle` 加成员 → 加进 `ValidStyleMap` 的 `FieldType.List` 那行 → 加进 `IsListStyle()`
   → 在 `GetString()` 的单元素摘要 `switch` 里加一个 case（只影响"列表恰好一条"时表格里的短标签，不加则显示 `[1]`）。
   ⚠️ `GetCopyData()` / `TryPasteData()` **不用动** —— 元素类型已进字段表就走通用编解码。
3. **编辑器窗口**：一般都要（见 C）。
4. **本地化**：`MST.{Style}`（枚举标签，放 `Core.xml`）；字段标题 `MST.{字段名}`，含义不直观再加 `MST.{字段名}Comment`。
5. **测试**：`Tests/SerializationCodecsTests.cs` 的 `MigratedTypes` 与 `字段声明逐个钉住` 各加一条；
   `Tests/FieldCodecsTests.cs` 的 `精确类型都能查到` 加一条 `InlineData(typeof(List<T>))`。

#### B. 类型本身进不了字段表（`IngredientCount` / `ThingFilter` / 多态列表）

只有三种情况走这条：元素带 `GetBaseCount` 语义（`IngredientCount`）、字段本身是过滤器（`ThingFilter`）、
元素是多态子类（`List<StockGenerator>`，见 [§7.6](#76-多态列表字段直接持有对象--列表窗口编辑stockgenlist)）。
在 `FieldCodecs.Build()` 里用 `Exact(typeof(List<T>), "诊断名", ExposeXxx, ResolveXxx)` 手写一对：

- 落盘：`ExposeList(data, meta, v => 对象列表 → List<string>)`；
- 读档：`ResolveList(data, meta, strList => (result, success))`，返回 `false` ⇒ 整字段标损坏、保持 Def 现状（**绝不写半截**）。

照 `ExposeIngredientList` / `ResolveIngredientList` 抄即可。

#### C. 新增编辑窗口的接线清单

1. 新建 `GUI/Editor/{Style}EditorWindow.cs`，继承 `ListEditorWindow<T>`，写**两个构造**：
   `(StatColumnConfig config, TweakData data)` 与 `(string title, List<T> initialList, Action<List<T>?> onSave)`。
2. 构造里把 `originalList` / `workingList` 换成**新实例**（`PocoCodec.CloneElement` 或逐字段复制）。
   直接引用字段里的对象 ⇒ 窗口里改到一半点取消，数据已经被改脏。
3. 实现 `AddButtonLabel` / `OnAddItem` / `DrawEntry`，需要时覆写 `IsItemValid`（无效行在保存时被丢弃）；
   行内宽度从 `rect.width` 里先扣掉删除按钮与间距再按比例分配，别写死像素。
4. **不要覆写 `SerializeList` / `DeserializeList`** —— 元素类型已进字段表时默认实现就是完整实现（存档、剪贴板、
   拖拽涂抹同源）。多覆写一个会被 `ListEditorWindowContractTests` 判成"多余转发"；确需覆写要登记进
   `IntentionalOverrides` 并写理由（目前只有 `StockGeneratorListEditorWindow`）。
5. `GUI/BalanceTweakGUI.cs` 的窗口工厂表加一行 `{ ColumnStyle.Xxx, (c, d) => new XxxEditorWindow(c, d) }`。
6. 本地化：`MST.AddXxx`（添加按钮）；选择类按钮优先复用已有的 `MST.SelectXxx`（`MST.SelectSkill` / `MST.SelectTerrain` …）。
7. 验收：`dotnet build` + `dotnet test` + `dotnet run --project Source/CheckLoc`（缺键、重复键、列宽一把过）。

### 7.5 添加本地化

任何字段需添加列标题翻译。对于含义不直观的字段，建议同时添加 `{FieldName}Comment` tooltip（StatDef由游戏提供Comment，因此无需说明）：

```xml
<MST.makeImmuneTo>免疫</MST.makeImmuneTo>
<MST.makeImmuneToComment>此阶段使角色免疫的疾病/健康状态列表。</MST.makeImmuneToComment>
```

`Comment` 后缀的 key 会自动作为 tooltip 显示在列标题上。

### 7.6 多态列表：字段直接持有对象 + 列表窗口编辑（StockGenList）

**目前只有 `StockGenList` 走这条路**（`TraderKindDef.stockGenerators`，`List<StockGenerator>`，原版 15 个子类）：
元素类型各不相同、每个子类的字段也不一样，没法用"一个固定列的列表编辑器"表达。

四条不变式，改这一列时别破：

- **字段值就是真实对象**，且字段与 Def 的活对象永不共享实例：`SetDef` 克隆进字段、`Apply` 再克隆一份交给 Def，
  所以"编辑 → 再编辑 → 重复 Apply"不会互相污染。

  ```csharp
  if (!IsFieldModified(nameof(stockGenerators)))
      stockGenerators ??= StockGeneratorCodec.CloneAll(d.stockGenerators);
  ApplyL<StockGenerator>(nameof(stockGenerators), ApplyStockGenerators);
  ```

- **字段表是唯一信息源**（`StockGeneratorCodec.ownFieldNames`，显式模式匹配原版类型）。表里没有的类型一律按"非原版"处理：
  **只读**（字段一个都不读不写，编辑 / 移动 / 删除全禁用），存档只留类型名占位，`Apply` 时按"类型相同 + 出现次序相同"
  从 Def 里找回原实例放回原位置 —— 改了这个商人再读档，既不会弄丢、也不会误改其它 Mod 的数据。
- **文本只出现在两个边界**（存档、剪贴板），由 `StockGeneratorCodec` 编码
  （`StockGenerator_Tag|countRange=1~4|tradeTag=ExoticMisc`，转义安全、**非 XML**）。编辑路径上没有任何文本格式 ——
  窗口直接读写对象字段；粘贴先整体解析，任一条失败即整块拒绝。
- `Apply` 写完列表后对每个生成器调 `ResolveReferences(def)`（`trader` 是 `[Unsaved]`，必须靠它写回）。

实现细节不在这里重复，读三处就够：`Data/StockGeneratorCodec.cs`（字段表 / 编解码 / 克隆 / `MergeWithDef`）、
`GUI/Editor/StockGeneratorFieldUi.cs`（按 `Kind` 画控件 + 字段标签字典，标签必须用显式字面量 —— CheckLoc 是静态正则，
看不见拼接出来的键）、`Tests/TweakDataSerializationTests.cs` 的 `testStockGen_*`
（`testStockGen_FieldTableIntegrity` 是"原版加类型 / 加字段"的第一道警报）。

### 7.7 构建验证

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
| `BiomePlantList` | 弹出窗口 | `BiomePlantListEditorWindow` | `List<BiomePlantRecord>?` |
| `BiomeAnimalList` | 弹出窗口 | `BiomeAnimalListEditorWindow` | `List<BiomeAnimalRecord>?` |
| `FishChanceList` | 弹出窗口 | `FishChanceListEditorWindow` | `List<FishChance>?` |
| `XenotypeChanceList` | 弹出窗口 | `XenotypeChanceListEditorWindow` | `List<XenotypeChance>?` |
| `DiseaseList` | 弹出窗口 | `DiseaseListEditorWindow` | `List<BiomeDiseaseRecord>?` |
| `WeatherCommonalityList` | 弹出窗口 | `WeatherCommonalityListEditorWindow` | `List<WeatherCommonalityRecord>?` |
| `TerrainThresholdList` | 弹出窗口 | `TerrainThresholdListEditorWindow` | `List<TerrainThreshold>?` |
| `StockGenList` | 弹出窗口 | `StockGeneratorListEditorWindow`（+ `StockGeneratorEditorWindow` / `SGComplexListEditorWindow`） | `List<StockGenerator>?`（字段即对象列表，字段表见 [§7.6](#76-多态列表字段直接持有对象--列表窗口编辑stockgenlist)） |
| `FactionCurveList` | 弹出窗口 | `FactionCurveListEditorWindow` | `List<FactionCurve>?`（每行 = 派系选择 + 曲线编辑按钮；`selectionWeightPerPointsCurve` 为 private，克隆/编解码走 PocoCodec 字段表） |
| `SkillRangeList` | 弹出窗口 | `SkillRangeListEditorWindow` | `List<SkillRange>?`（每行 = 技能选择 + 最低/最高等级；`skill` / `range` 均为 private，经 PocoCodec `ReadField` / `WriteField` 读写） |
| `DefSelector` | 弹出窗口 | `DefSelectionWindow` | `T? where T : Def` |