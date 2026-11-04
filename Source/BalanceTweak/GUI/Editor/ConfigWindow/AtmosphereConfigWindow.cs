namespace BalanceTweak;

/// <summary>
/// Atmosphere Overhaul 的全部扩展数据统一编辑窗口 —— 地形 / 伤害 / 服饰 / 健康 / 种族
/// 五个页签共用同一个窗口类，各自只留一个只读触发列。
///
/// <para>
/// <b>为什么不再逐列展示</b>：这五组字段一共 18 格，但每一行几乎只命中一两格
/// （挂了 <c>AtmosphereTerrainExtension</c> 的地形只占全部地形的极小一部分）。
/// 逐列展示等于给每个页签永久挤进去十几列恒空白，列宽与列序还被占住；
/// 而「同一页签里再开一个专属页签」只是把同样的问题换个地方。做法与 Fortified 的
/// <see cref="RelayConfigWindow"/> 一致：一个触发列 + 一个窗口，字段全部 <c>NotAColumn</c>。
/// </para>
///
/// <para>
/// <b>一个类服务五个宿主</b>：各宿主只声明自己那几格，窗口按字段名解析 ——
/// 宿主上没有的行整行跳过、不占高度（<see cref="ConfigWindow"/> 构造时已缓存成字典）。
/// 所以这里用 <c>d =&gt; d is XxxData</c> 分节就够了，不需要再判「这个 Def 有没有扩展」：
/// 没有扩展时触发列本身被 <c>Hidden</c> 判空，玩家根本点不进来。
/// </para>
///
/// <para>
/// <b>P3 的建筑不需要另起窗口</b>：往 <see cref="Sections"/> 末尾加一节
/// <c>ConfigSection.Of(…, d =&gt; d is BuildingData, …)</c>，再在 <see cref="TitleOf"/> 里补一行即可。
/// </para>
/// </summary>
public class AtmosphereConfigWindow : ConfigWindow
{
    // internal 而不是 private：测试要逐行核对「这一行落在这个宿主真实存在的字段上」，
    // 写错时编译期全绿、运行期只是那一行永远不出现（见 AtmosphereConfigWindowTests）。
    internal static readonly ConfigSection[] Sections =
    {
        // ── 地形：AtmosphereTerrainExtension（Space / HotSpring / LavaDeep…）──
        // 六格全部被 GroundMaterialTable.ResolveCache 固化成结构体，改完必须清缓存
        // （由 TerrainData.ApplyAoExtension 负责，这里不做）。
        ConfigSection.Of(null, d => d is TerrainData,
                ConfigRow.Enum(nameof(TerrainData.mode)),
                ConfigRow.Float(nameof(TerrainData.fixedTemperature), -300f, 300f),
                ConfigRow.Float(nameof(TerrainData.minTemperature), -300f, 300f),
                ConfigRow.Float(nameof(TerrainData.maxTemperature), -300f, 300f),
                ConfigRow.Percent(nameof(TerrainData.albedo)),
                ConfigRow.Percent(nameof(TerrainData.thermalRate)))
            .Note("MST.aoTerrainNote"),

        // ── 伤害：AtmosphereBlastExtension（AO_Blast 之类的爆炸伤害）──
        ConfigSection.Of(null, d => d is DamageData,
                ConfigRow.Float(nameof(DamageData.earThresholdKPa), 0f, 5000f),
                ConfigRow.Float(nameof(DamageData.earCertainKPa), 0f, 5000f),
                ConfigRow.Percent(nameof(DamageData.hearingLossChanceAtFull)),
                ConfigRow.Float(nameof(DamageData.lungThresholdKPa), 0f, 5000f),
                ConfigRow.Float(nameof(DamageData.lungDestroyedKPa), 0f, 5000f))
            .Note("MST.aoBlastNote"),

        // ── 服饰：AtmosphereApparelExtension（氧气面罩 / 压力服）──
        // 前两格是三态 int：1 = 强制是，0 = 强制不是，-1 = 自动判断。0 与 -1 语义不同。
        ConfigSection.Of(null, d => d is ApparelData,
                ConfigRow.Int(nameof(ApparelData.breathingSupply), -1, 1),
                ConfigRow.Int(nameof(ApparelData.pressureShell), -1, 1),
                ConfigRow.Float(nameof(ApparelData.capacityAirTanks), -1f, 1000f),
                ConfigRow.Float(nameof(ApparelData.suitPressureHpa), 0f, 3000f))
            .Note("MST.aoApparelNote"),

        // ── 健康：HearingAftermathExtension（AO_Barotrauma）──
        ConfigSection.Of(null, d => d is HediffData,
                ConfigRow.Float(nameof(HediffData.ruptureSeverity), 0f, 100f),
                ConfigRow.Percent(nameof(HediffData.chancePerEar)))
            .Note("MST.aoHearingNote"),

        // ── 种族：AtmospherePawnExtension ──
        // AO 自己没给任何种族挂这个扩展（只在 XML 注释里提了一句），所以装了 AO 而没有
        // 别的兼容 Mod 时，这一行的触发列恒为空白 —— 保留它只为接口对齐。
        ConfigSection.Of(null, d => d is RaceData,
                ConfigRow.Float(nameof(RaceData.blastToleranceFactor), 0f, 10f))
            .Note("MST.aoRaceNote"),

        // ══════ 以下是建筑页（P3）：按"这个 Def 实际挂了哪些 Comp"分节 ══════
        // 判据一律是 AoBuilding 里的静态方法组 —— 它们只碰 object / Def，不碰 AO 的类型。

        // ── 风扇（CompProperties_SingleFieldFan）：4 个风扇共用一个 Comp ──
        ConfigSection.Of("MST.aoFanSection", AoBuilding.HasFan,
                ConfigRow.Enum(nameof(BuildingData.fanPowerSource)),
                ConfigRow.Float(nameof(BuildingData.fanMaxPowerWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.fanDefaultPowerWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.fanRatedFreeAirFlow), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.fanOutletAreaM2), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.fanWindResponsePerTick), 0f, 10f),
                ConfigRow.Bool(nameof(BuildingData.fanAllowOutputAdjustment)),
                ConfigRow.Bool(nameof(BuildingData.fanAllowEasyRotation)),
                ConfigRow.Float(nameof(BuildingData.fanStoredEnergyCapacityJoules), 0f, 10000000f),
                ConfigRow.Float(nameof(BuildingData.fanInitialStoredEnergyJoules), 0f, 10000000f))
            .Note("MST.aoFanNote"),

        // ── 涡轮喷气（CompProperties_PairedJet）──
        ConfigSection.Of("MST.aoJetSection", AoBuilding.HasJet,
                ConfigRow.Float(nameof(BuildingData.jetFuelPerSecond), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.jetWaterKgPerFuel), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetMaxSpeed), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.jetHalfSpeed), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.jetMaxTemperature), -273f, 3000f),
                ConfigRow.Float(nameof(BuildingData.jetMaxPressureRatio), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetHalfPressureRatio), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetInletPressureRatio), 0f, 10f),
                ConfigRow.Float(nameof(BuildingData.jetOxygenPercent), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetColumnHeight), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetOxygenConsumptionPercent), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.jetFlameoutOxygenKPa), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.jetRelightOxygenKPa), 0f, 1000f),
                ConfigRow.Bool(nameof(BuildingData.jetAllowEasyRotation)))
            .Note("MST.aoJetNote"),

        // ── 空压机 / PSA（CompProperties_AirCompressor）──
        ConfigSection.Of("MST.aoCompressorSection", AoBuilding.HasCompressor,
                ConfigRow.Float(nameof(BuildingData.compressorIntakeM3PerTick), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.compressorRunningWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.compressorIdleWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.compressorMaxGoalKPa), 0f, 10000f),
                ConfigRow.Bool(nameof(BuildingData.compressorSeparateOxygen)),
                ConfigRow.Def(nameof(BuildingData.compressorProductDef)),
                ConfigRow.Def(nameof(BuildingData.compressorOxygenProductDef)))
            .Note("MST.aoCompressorNote"),

        // ── 气瓶（CompProperties_GasTank）──
        ConfigSection.Of("MST.aoGasTankSection", AoBuilding.HasGasTank,
                ConfigRow.Float(nameof(BuildingData.gasTankOxygenPercent), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.gasTankExplodeAboveCelsius), -273f, 3000f))
            .Note("MST.aoGasTankNote"),

        // ── 加湿器（CompProperties_Humidifier）──
        ConfigSection.Of("MST.aoHumidifierSection", AoBuilding.HasHumidifier,
                ConfigRow.Float(nameof(BuildingData.humidifierMaxKgPerHour), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.humidifierRunningWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.humidifierIdleWatts), 0f, 100000f))
            .Note("MST.aoHumidifierNote"),

        // ── 除湿机（CompProperties_Dehumidifier）──
        ConfigSection.Of("MST.aoDehumidifierSection", AoBuilding.HasDehumidifier,
                ConfigRow.Float(nameof(BuildingData.dehumidifierSweptVolumes), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.dehumidifierRunningWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.dehumidifierIdleWatts), 0f, 100000f))
            .Note("MST.aoDehumidifierNote"),

        // ── 发条（CompProperties_Mainspring）──
        ConfigSection.Of("MST.aoMainspringSection", AoBuilding.HasMainspring,
                ConfigRow.Int(nameof(BuildingData.mainspringWindingTicks), 1, 100000),
                ConfigRow.Bool(nameof(BuildingData.mainspringAutoRewind)))
            .Note("MST.aoMainspringNote"),

        // ── 集雨器（CompProperties_RainCollector）──
        ConfigSection.Of("MST.aoRainCollectorSection", AoBuilding.HasRainCollector,
                ConfigRow.Float(nameof(BuildingData.rainCollectorCatchmentM2), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.rainCollectorCapacityUnits), 0f, 100000f))
            .Note("MST.aoRainCollectorNote"),

        // ── 灌溉器（CompProperties_Irrigator）──
        ConfigSection.Of("MST.aoIrrigatorSection", AoBuilding.HasIrrigator,
                ConfigRow.Float(nameof(BuildingData.irrigatorSprayRadius), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.irrigatorPumpRadius), 0f, 100f),
                ConfigRow.Int(nameof(BuildingData.irrigatorSprayTicks), 0, 100000),
                ConfigRow.Float(nameof(BuildingData.irrigatorSessionMm), 0f, 1000f),
                ConfigRow.Int(nameof(BuildingData.irrigatorCheckIntervalTicks), 0, 100000),
                ConfigRow.Int(nameof(BuildingData.irrigatorRestTicks), 0, 100000),
                ConfigRow.Float(nameof(BuildingData.irrigatorSprayWatts), 0f, 100000f),
                ConfigRow.Float(nameof(BuildingData.irrigatorIdleWatts), 0f, 100000f))
            .Note("MST.aoIrrigatorNote"),

        // ── 建筑大气属性（BuildingAtmosphereExtension）：原版建筑唯一可改的一组 ──
        ConfigSection.Of("MST.aoBuildingExtSection", AoBuilding.HasAtmosphereExtension,
                ConfigRow.Float(nameof(BuildingData.buildingPressureTolerance), 0f, 1000000f),
                ConfigRow.Float(nameof(BuildingData.buildingWindTolerance), 0f, 1000f),
                ConfigRow.Float(nameof(BuildingData.buildingAirLeak), 0f, 100f),
                ConfigRow.Float(nameof(BuildingData.buildingHeatExchange), 0f, 100f),
                ConfigRow.Bool(nameof(BuildingData.buildingAirPassable)),
                ConfigRow.Float(nameof(BuildingData.buildingAirFillPercent), -1f, 1f))
            .Note("MST.aoBuildingExtNote"),
    };

    public AtmosphereConfigWindow(TweakData data)
        : base(data, TitleOf(data), "MST.aoConfigIntro", Sections, 380f, "MST.aoConfigNote")
    {
    }

    /// <summary>
    /// 窗口标题直接复用触发列的表头（<c>MST.{字段名}</c>）：标题因此是「HotSpring - 大气地形」，
    /// 与玩家刚点的那个按钮同名，不需要再为每个宿主多备一份 Title key。
    /// </summary>
    private static string TitleOf(TweakData data)
    {
        if (data is TerrainData) return "MST.aoTerrainConfig";
        if (data is DamageData) return "MST.aoBlastConfig";
        if (data is ApparelData) return "MST.aoApparelConfig";
        if (data is HediffData) return "MST.aoHearingConfig";
        if (data is RaceData) return "MST.aoRaceConfig";
        if (data is BuildingData) return "MST.aoBuildingConfig";
        return "MST.aoConfig";
    }
}
