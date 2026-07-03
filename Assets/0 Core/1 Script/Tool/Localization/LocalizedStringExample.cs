using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using XFramework;

/// <summary>
/// LocalizedString 三种用法范例（A 一次性取值 / B 随语言自动刷新 / C 纯代码动态切 key）。
/// 演示「不挂 LocalizeStringEvent 组件、把绑定关系留在 C# 里」的写法。
/// 表名走 LocalizeTableSet，占位符名走 LocalizeVarSet，避免硬编码字符串。
/// 订阅一律用具名方法（不用 lambda）：MonoBehaviour 自带生命周期，OnEnable 里 += 具名方法，OnDisable 里对称 -= 掉即可。
/// 注意：下面用到的 key（CrashBetRange / WinTip 等）请按需替换成字符串表里真实存在的 key。
/// </summary>
public class LocalizedStringExample : MonoBehaviour
{
    [SerializeField] TMP_Text oneShotText;   // 演示 A：一次性
    [SerializeField] TMP_Text rangeText;     // 演示 B：自动刷新
    [SerializeField] TMP_Text dynamicText;   // 演示 C：动态切 key

    // B 用：在 C# 里声明引用（Inspector 也能直接选表和 key），不依赖任何组件
    [SerializeField] LocalizedString betRange;   // 例："最低金额{MinBet} 最高金额{MaxBet}"

    void OnEnable()
    {
        ExampleA();
        ExampleB_Bind();
        ExampleC();
    }

    void OnDisable()
    {
        // B 的绑定必须在这里解除，否则对象销毁后回调仍持有引用 → 泄漏 / 空引用。
        // 用同一个具名方法 -= 掉即可，不需要额外保存退订句柄。
        betRange.StringChanged -= OnBetRangeChanged;
    }

    // ───────────────────────── A. 一次性取值（切语言不会自动更新） ─────────────────────────
    void ExampleA()
    {
        // 同步：确定表已加载时用，最简单。项目里 LanguageManager 已封装一层
        string text = LanguageManager.Instance.GetLocalizedString(
            LocalizeTableSet.CasinoGame, "CrashBetRange");
        oneShotText.text = text;

        // 异步：更安全，首次访问某张表可能尚未加载完成。回调用具名方法，不用 lambda
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
            LocalizeTableSet.CasinoGame, "CrashBetRange");
        op.Completed += OnOneShotLoaded;
    }

    // 异步加载完成回调：一次性写回，用完不再关心（AsyncOperationHandle 结束后自身不再触发）
    void OnOneShotLoaded(AsyncOperationHandle<string> handle) => oneShotText.text = handle.Result;

    // ───────────────────────── B. 随语言切换自动刷新（组件的「代码版」） ─────────────────────────
    // 这就是「把绑定关系留在 C# 里」：声明 LocalizedString → 订阅 StringChanged → 回调里写回 TMP。
    // 效果与 prefab 上挂 LocalizeStringEvent 完全一致，但绑定写在代码里，可被抽象、可换后端。
    void ExampleB_Bind()
    {
        // 直接订阅具名方法，退订在 OnDisable 里对称 -= 掉
        betRange.StringChanged += OnBetRangeChanged;

        // 灌占位符。前几个 refresh:false 不触发刷新，最后一个统一刷新一次（少算几遍）
        betRange.SetVar(LocalizeVarSet.CrashSprint.MinBet, 100, false);
        betRange.SetVar(LocalizeVarSet.CrashSprint.MaxBet, 9999);   // 触发 StringChanged → OnBetRangeChanged 写回
    }

    // 语言/占位符变化时写回 TMP。具名方法既能 += 又能 -=，语言切换会自动再次回调
    void OnBetRangeChanged(string value) => rangeText.text = value;

    // ───────────────────────── C. 纯代码动态切 key（无固定字段） ─────────────────────────
    void ExampleC()
    {
        // 运行时凭参数决定指向哪条 key，不需要预先在 Inspector 里挂
        LocalizedString ls = new LocalizedString
        {
            TableReference = LocalizeTableSet.CasinoGame,
            TableEntryReference = "WinTip",   // 例："恭喜获得{Payout}金币"
        };
        ls.SetVar(LocalizeVarSet.CrashSprint.Payout, 12000, false);

        // 直接取当前语言的成品串（一次性）
        dynamicText.text = ls.GetLocalizedString();

        // 若也想自动刷新，同样订阅一个具名方法即可（记得在 OnDisable 退订）：
        // ls.StringChanged += OnBetRangeChanged;   // 任意 void(string) 方法都能复用
    }
}
