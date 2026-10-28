using BalanceTweak;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 批量运算「步长取整」的语义：<c>(值 + 增减) × 倍率 → 按步长取整 → 上下限截断</c>。
///
/// <para>
/// 这几个开关是<b>全局静态字段</b>（和批量区的其它输入框一样），xunit 已关并行
/// （见 <c>AssemblyInfo.cs</c>），但仍要在每个测试前后还原，免得把状态漏给别的类。
/// </para>
/// </summary>
public class BatchStepTests : System.IDisposable
{
    private const float Step = 0.5f;

    private readonly float _flat = BalanceTweakSettings.batchFlat;
    private readonly float _prec = BalanceTweakSettings.batchPrec;
    private readonly float _lower = BalanceTweakSettings.batchLowerLimit;
    private readonly float _upper = BalanceTweakSettings.batchUpperLimit;
    private readonly bool _lowerOn = BalanceTweakSettings.batchLowerLimitEnabled;
    private readonly bool _upperOn = BalanceTweakSettings.batchUpperLimitEnabled;
    private readonly float _step = BalanceTweakSettings.batchStep;
    private readonly bool _stepOn = BalanceTweakSettings.batchStepEnabled;
    private readonly BalanceTweakSettings.BatchStepMode _mode = BalanceTweakSettings.batchStepMode;

    public BatchStepTests()
    {
        BalanceTweakSettings.batchFlat = 0f;
        BalanceTweakSettings.batchPrec = 1f;
        BalanceTweakSettings.batchLowerLimit = 0f;
        BalanceTweakSettings.batchUpperLimit = 10000f;
        BalanceTweakSettings.batchLowerLimitEnabled = false;
        BalanceTweakSettings.batchUpperLimitEnabled = false;
        BalanceTweakSettings.batchStep = 0f;
        BalanceTweakSettings.batchStepEnabled = false;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Round;
    }

    public void Dispose()
    {
        BalanceTweakSettings.batchFlat = _flat;
        BalanceTweakSettings.batchPrec = _prec;
        BalanceTweakSettings.batchLowerLimit = _lower;
        BalanceTweakSettings.batchUpperLimit = _upper;
        BalanceTweakSettings.batchLowerLimitEnabled = _lowerOn;
        BalanceTweakSettings.batchUpperLimitEnabled = _upperOn;
        BalanceTweakSettings.batchStep = _step;
        BalanceTweakSettings.batchStepEnabled = _stepOn;
        BalanceTweakSettings.batchStepMode = _mode;
    }

    private static float Apply(float value) => BalanceTweakSettings.ApplyBatchToValue(value);

    [Fact]
    public void 开关关闭时不取整()
    {
        BalanceTweakSettings.batchStep = Step;
        BalanceTweakSettings.batchStepEnabled = false;
        Assert.Equal(40.2345f, Apply(40.2345f), 3);
    }

    [Fact]
    public void 开关打开但步长为零等于不取整()
    {
        BalanceTweakSettings.batchStep = 0f;
        BalanceTweakSettings.batchStepEnabled = true;
        Assert.Equal(40.2345f, Apply(40.2345f), 3);
    }

    [Fact]
    public void 步长0点5加四舍五入_按最近的倍数靠()
    {
        BalanceTweakSettings.batchStep = Step;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Round;

        Assert.Equal(40.0f, Apply(40.2f), 3);   // 80.4 → 80
        Assert.Equal(40.5f, Apply(40.3f), 3);   // 80.6 → 81
    }

    [Fact]
    public void 步长50加向下取整()
    {
        BalanceTweakSettings.batchStep = 50f;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Floor;

        Assert.Equal(300f, Apply(325f), 3);     // 6.5 → 6
        Assert.Equal(350f, Apply(375f), 3);     // 7.5 → 7
    }

    [Fact]
    public void 步长50加向上取整()
    {
        BalanceTweakSettings.batchStep = 50f;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Ceil;

        Assert.Equal(350f, Apply(325f), 3);     // 6.5 → 7
        Assert.Equal(400f, Apply(351f), 3);     // 7.02 → 8
    }

    /// <summary>
    /// 「四舍五入」必须是远离零的舍入。0.5 的步长下 40.25 正好落在两个倍数中间：
    /// 银行家舍入（Mathf.Round）会舍到偶数 40.0，这里要的是 40.5。
    /// </summary>
    [Fact]
    public void 四舍五入取远离零而不是银行家舍入()
    {
        BalanceTweakSettings.batchStep = Step;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Round;

        Assert.Equal(40.5f, Apply(40.25f), 3);  // 80.5 → 81（银行家舍入会给 80）
    }

    /// <summary>
    /// 取整必须发生在上下限截断<b>之前</b>：上下限是硬边界。
    /// 320 + 步长 50 向上取整 → 350，再被上限 325 压回 325；
    /// 如果先截断再取整，结果会是 350，越过了上限。
    /// </summary>
    [Fact]
    public void 取整在上下限截断之前()
    {
        BalanceTweakSettings.batchStep = 50f;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Ceil;
        BalanceTweakSettings.batchUpperLimit = 325f;
        BalanceTweakSettings.batchUpperLimitEnabled = true;

        Assert.Equal(325f, Apply(320f), 3);
    }

    [Fact]
    public void 增减与倍率先算再取整()
    {
        BalanceTweakSettings.batchFlat = 0.1f;
        BalanceTweakSettings.batchPrec = 2f;
        BalanceTweakSettings.batchStep = Step;
        BalanceTweakSettings.batchStepEnabled = true;
        BalanceTweakSettings.batchStepMode = BalanceTweakSettings.BatchStepMode.Round;

        // (40 + 0.1) * 2 = 80.2 → 160.4 → 160 → 80
        Assert.Equal(80f, Apply(40f), 3);
    }
}
