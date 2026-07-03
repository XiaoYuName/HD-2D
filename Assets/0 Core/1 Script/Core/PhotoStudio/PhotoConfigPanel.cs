using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 拍照配置面板：在限时内配置 风格(光影)/背景/姿势/衣服 四项，实时更新预览，超时或点击开始进入对焦小游戏
public class PhotoConfigPanel : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] PhotoStudioGameConfig config;
    [Tooltip("预览：复用 PhotoSceneConfigPanel 预制体实时渲染背景/角色/光影")]
    [SerializeField] PhotoSceneConfigPanel previewPanel;
    [Tooltip("倒计时显示：CountDownPop 预制体")]
    [SerializeField] CountDownPop countDownPop;

    [Header("选择项文本")]
    [SerializeField] TextMeshProUGUI styleNameText;     // 风格(光影)
    [SerializeField] TextMeshProUGUI bgNameText;        // 背景
    [SerializeField] TextMeshProUGUI postureNameText;   // 姿势
    [SerializeField] TextMeshProUGUI clothesNameText;   // 衣服

    [Header("选择项名称多语言")]
    [SerializeField] LocalizeStringEvent styleNameLocalize;
    [SerializeField] LocalizeStringEvent bgNameLocalize;
    [SerializeField] LocalizeStringEvent postureNameLocalize;
    [SerializeField] LocalizeStringEvent clothesNameLocalize;

    [Header("切换按钮")]
    [SerializeField] Button stylePrevBtn, styleNextBtn;
    [SerializeField] Button bgPrevBtn, bgNextBtn;
    [SerializeField] Button posturePrevBtn, postureNextBtn;
    [SerializeField] Button clothesPrevBtn, clothesNextBtn;

    [Header("其它UI")]
    [SerializeField] TextMeshProUGUI apCostText;        // 消耗体力文本
    [SerializeField] LocalizeStringEvent apCostLocalize; // 消耗体力多语言（用于注入数值参数）
    [SerializeField] Button startButton;
    [SerializeField] Button closeButton;

    // (风格, 背景, 姿势, 衣服, 解析出的角色图)
    public event Action<PhotoSceneConfigInfo> OnStartGame;
    public event Action OnClose;

    // 各类配置的有序ID列表（用于左右切换），以及当前游标索引
    List<long> lightingIds, bgIds, postureIds, clothesIds;
    int styleIndex, bgIndex, postureIndex, clothesIndex;
    Coroutine countDownCt;

    void Awake()
    {
        Bind(stylePrevBtn, StylePrev);
        Bind(styleNextBtn, StyleNext);
        Bind(bgPrevBtn, BgPrev);
        Bind(bgNextBtn, BgNext);
        Bind(posturePrevBtn, PosturePrev);
        Bind(postureNextBtn, PostureNext);
        Bind(clothesPrevBtn, ClothesPrev);
        Bind(clothesNextBtn, ClothesNext);
        Bind(startButton, StartGame);
        Bind(closeButton, Close);

        // 兜底：保证每个名称 LocalizeStringEvent 的 OnUpdateString 会把结果写回对应 TMP 文本，
        // 否则切换 key 后内部引用变了、文本却不刷新。
        EnsureLocalizeBinding(styleNameLocalize, styleNameText);
        EnsureLocalizeBinding(bgNameLocalize, bgNameText);
        EnsureLocalizeBinding(postureNameLocalize, postureNameText);
        EnsureLocalizeBinding(clothesNameLocalize, clothesNameText);
    }

    static void Bind(Button btn, UnityEngine.Events.UnityAction action)
    {
        btn.onClick.AddListener(action);
    }

    static void EnsureLocalizeBinding(LocalizeStringEvent lse, TextMeshProUGUI text)
    {
        // 运行时再接一次 SetText；即使 Inspector 里已持久绑定，至多重复写同值，无副作用。
        lse.OnUpdateString.AddListener(text.SetText);
    }

    void OnEnable()
    {
        OpenConfig();
    }

    void OnDisable()
    {
        StopCountDown();
    }

    // 打开配置：重置为默认项、刷新预览、开始限时倒计时
    public void OpenConfig()
    {
        lightingIds = new List<long>(config.LightingPresets.Keys);
        bgIds = new List<long>(config.SceneBgConfigs.Keys);
        postureIds = new List<long>(config.PostureConfigs.Keys);
        clothesIds = new List<long>(config.ClothesConfigs.Keys);

        styleIndex = IndexOfId(lightingIds, config.DefaultLightingId);
        bgIndex = IndexOfId(bgIds, config.DefaultBgId);
        postureIndex = IndexOfId(postureIds, config.DefaultPostureId);
        clothesIndex = IndexOfId(clothesIds, config.DefaultClothesId);

        RefreshAll();

        // 消耗体力
        apCostLocalize.SetVar(LocVarSet.MiniGame.SpConsumeCount, config.PhotoCosumeAp);
        StartCountDown();
    }

    #region 选择切换
    [Button] public void StylePrev() => ChangeStyle(-1);
    [Button] public void StyleNext() => ChangeStyle(1);
    [Button] public void BgPrev() => ChangeBg(-1);
    [Button] public void BgNext() => ChangeBg(1);
    [Button] public void PosturePrev() => ChangePosture(-1);
    [Button] public void PostureNext() => ChangePosture(1);
    [Button] public void ClothesPrev() => ChangeClothes(-1);
    [Button] public void ClothesNext() => ChangeClothes(1);

    void ChangeStyle(int delta)
    {
        styleIndex = Wrap(styleIndex + delta, Count(lightingIds));
        RefreshAll();
    }

    void ChangeBg(int delta)
    {
        bgIndex = Wrap(bgIndex + delta, Count(bgIds));
        RefreshAll();
    }

    void ChangePosture(int delta)
    {
        postureIndex = Wrap(postureIndex + delta, Count(postureIds));
        RefreshAll();
    }

    void ChangeClothes(int delta)
    {
        clothesIndex = Wrap(clothesIndex + delta, Count(clothesIds));
        RefreshAll();
    }

    static int Wrap(int index, int count)
    {
        if(count <= 0)
            return 0;
        return ((index % count) + count) % count;
    }

    static int Count(List<long> ids) => ids != null ? ids.Count : 0;

    static int IndexOfId(List<long> ids, long id)
    {
        if(ids == null)
            return 0;
        int i = ids.IndexOf(id);
        return i >= 0 ? i : 0;
    }

    // 取当前游标对应的ID（列表为空时返回 0）
    static long CurId(List<long> ids, int index)
        => (ids != null && ids.Count > 0) ? ids[Mathf.Clamp(index, 0, ids.Count - 1)] : 0L;

    long CurLightingId => CurId(lightingIds, styleIndex);
    long CurBgId => CurId(bgIds, bgIndex);
    long CurPostureId => CurId(postureIds, postureIndex);
    long CurClothesId => CurId(clothesIds, clothesIndex);
    #endregion

    // 刷新四项文本与预览
    void RefreshAll()
    {
        PhotoLightingPreset lighting = config.GetLighting(CurLightingId);
        PhotoSceneBgConfig bg = config.GetSceneBg(CurBgId);
        PhotoPostureConfigItem posture = config.GetPosture(CurPostureId);
        PhotoClothesConfigItem clothes = config.GetClothes(CurClothesId);

        SetLocalizedName(styleNameLocalize, lighting.name);
        SetLocalizedName(bgNameLocalize, bg.name);
        SetLocalizedName(postureNameLocalize, posture.name);
        SetLocalizedName(clothesNameLocalize, clothes.name);
        RefreshPreview();
    }
    void SetLocalizedName(LocalizeStringEvent lse, string key)
    {
       lse.StringReference.SetReference(LocTableSet.PhotoStudio, key);
       lse.RefreshString();
    }

    // 配置实时更新预览（背景 + 角色 + 光影）
    void RefreshPreview()
    {
        previewPanel.SelectBackground(CurBgId);
        previewPanel.SetCharacterSprite(ResolveCharacterSprite());
        previewPanel.ApplyLighting(CurLightingId);
    }

    // 姿势 + 服装 组合解析角色图
    Sprite ResolveCharacterSprite()
    {
        return config.GetCharacterSprite(CurPostureId, CurClothesId);
    }

    #region 倒计时
    void StartCountDown()
    {
        StopCountDown();
        countDownCt = StartCoroutine(CountDownCt());
    }

    void StopCountDown()
    {
        if(countDownCt != null)
        {
            StopCoroutine(countDownCt);
            countDownCt = null;
        }
    }

    IEnumerator CountDownCt()
    {
        float timeLeft = config.CameraConfigCountDownTime;
        countDownPop.SetText(Mathf.CeilToInt(timeLeft));  

        while(timeLeft > 0f)
        {
            yield return null;
            timeLeft -= Time.deltaTime;
            if(countDownPop != null)
                countDownPop.SetText(Mathf.CeilToInt(Mathf.Max(timeLeft, 0f)));
        }

        // 限时结束：用当前配置自动开始拍摄
        StartGame();
    }
    #endregion

    // 点击【开始游戏】或倒计时结束触发
    [Button("开始游戏")]
    public void StartGame()
    {
        StopCountDown();

        PhotoSceneConfigInfo result = new()
        {
            bgId = CurBgId,
            lightingId = CurLightingId,
            postureId = CurPostureId,
            clothesId = CurClothesId,
            characterSprite = ResolveCharacterSprite(),
        };
        OnStartGame?.Invoke(result);
    }

    [Button("关闭")]
    public void Close()
    {
        StopCountDown();
        OnClose?.Invoke();
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    [Button("创建拍照配置面板UI", ButtonSizes.Large)]
    void CreateConfigPanelUI()
    {
        if(startButton != null || styleNameText != null)
        {
            Debug.LogWarning("[PhotoConfigPanel] UI 已存在，若要重建请先清空已绑定的引用。");
            return;
        }

        RectTransform root = transform as RectTransform;
        if(root == null)
            root = Undo.AddComponent<RectTransform>(gameObject);
        root.sizeDelta = new Vector2(860f, 520f);
        root.anchoredPosition = Vector2.zero;

        Image windowBg = UICreateTool.CreateImage("WindowBg", root, new Color(0.78f, 0.80f, 0.82f, 1f));
        UIAnchorTool.Stretch(windowBg.rectTransform);

        // 标题
        TextMeshProUGUI title = UICreateTool.CreateText("Title", root, "拍照配置", 30f);
        title.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(title.rectTransform, new Vector2(30f, -24f), new Vector2(240f, 44f));
        LocTool.AttachText(title, PhotoStudioTable, "PhotoConfig", "拍照配置");

        // 关闭按钮（右上角，点击事件在 Awake 中绑定）
        Button closeBtn = UICreateTool.CreateButton("CloseButton", root, "X", new Color(0.86f, 0.30f, 0.30f, 1f), out _);
        RectTransform closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.sizeDelta = new Vector2(48f, 48f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);

        // 四个选择行（切换按钮使用 PhotoConfigPanelItemSwitchButton 预制体，左侧 Rotation z=180）
        float rowY = -96f;
        const float rowStep = 70f;
        styleNameText = CreateSelectorRow(root, rowY, "风格选择", "StyleSelect", out stylePrevBtn, out styleNextBtn, out styleNameLocalize);
        bgNameText = CreateSelectorRow(root, rowY - rowStep, "背景选择", "BgSelect", out bgPrevBtn, out bgNextBtn, out bgNameLocalize);
        postureNameText = CreateSelectorRow(root, rowY - rowStep * 2f, "姿势选择", "PostureSelect", out posturePrevBtn, out postureNextBtn, out postureNameLocalize);
        clothesNameText = CreateSelectorRow(root, rowY - rowStep * 3f, "衣服选择", "ClothesSelect", out clothesPrevBtn, out clothesNextBtn, out clothesNameLocalize);

        // 预览区（右侧）
        TextMeshProUGUI previewTitle = UICreateTool.CreateText("PreviewTitle", root, "预览效果", 20f);
        previewTitle.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(previewTitle.rectTransform, new Vector2(500f, -90f), new Vector2(200f, 28f));
        LocTool.AttachText(previewTitle, PhotoStudioTable, "PreviewEffect", "预览效果");

        Image previewFrame = UICreateTool.CreateImage("PreviewFrame", root, new Color(0.30f, 0.32f, 0.30f, 1f));
        UIAnchorTool.TopLeft(previewFrame.rectTransform, new Vector2(500f, -120f), new Vector2(320f, 240f));
        // 内嵌 PhotoSceneConfigPanel 预制体作为实时预览
        GameObject previewGo = InstantiatePrefab(GuidPhotoSceneConfigPanel, previewFrame.rectTransform);
        if(previewGo != null)
        {
            UIAnchorTool.Stretch((RectTransform)previewGo.transform);
            previewPanel = previewGo.GetComponent<PhotoSceneConfigPanel>();
        }

        // 提示文本（底部左）
        TextMeshProUGUI hint = UICreateTool.CreateText("HintText", root, "选择4项配置后开始拍摄吧!", 20f);
        hint.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(hint.rectTransform, new Vector2(30f, -430f), new Vector2(360f, 36f));
        LocTool.AttachText(hint, PhotoStudioTable, "ConfigHint", "选择4项配置后开始拍摄吧!");

        // 开始游戏按钮（点击事件在 Awake 中绑定）
        Button startBtn = UICreateTool.CreateButton("StartButton", root, "开始游戏", new Color(0.55f, 0.85f, 0.45f, 1f), out TextMeshProUGUI startLabel);
        RectTransform startRt = startBtn.GetComponent<RectTransform>();
        UIAnchorTool.TopLeft(startRt, new Vector2(430f, -424f), new Vector2(150f, 52f));
        LocTool.AttachText(startLabel, PhotoStudioTable, "StartGame", "开始游戏");
        startButton = startBtn;

        // 消耗体力文本
        TextMeshProUGUI cost = UICreateTool.CreateText("ApCostText", root, "消耗-1体力", 20f);
        cost.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(cost.rectTransform, new Vector2(600f, -430f), new Vector2(200f, 36f));
        apCostText = cost;
        apCostLocalize = LocTool.AttachText(cost, PhotoStudioTable, "ConsumeSp", "消耗{SpConsumeCount}体力");

        closeButton = closeBtn;

        // 倒计时 CountDownPop（顶部居中）
        GameObject popGo = InstantiatePrefab(GuidCountDownPop, root);
        if(popGo != null)
        {
            RectTransform popRt = (RectTransform)popGo.transform;
            popRt.anchorMin = popRt.anchorMax = new Vector2(0.5f, 1f);
            popRt.pivot = new Vector2(0.5f, 1f);
            popRt.anchoredPosition = new Vector2(0f, 28f);
            countDownPop = popGo.GetComponent<CountDownPop>();
        }

        EditorUtility.SetDirty(this);
        if(!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[PhotoConfigPanel] 拍照配置面板UI已创建并自动绑定字段。");
    }

    const string GuidPhotoSceneConfigPanel = "2ad0600905b2ef040bb067822eb9e71d";
    const string GuidCountDownPop = "d887aeafd7baf7f46b9e0ac4072326c8";
    const string GuidSwitchButton = "8bfb2f06b930b944899c48246a909e24";
    const string PhotoStudioTable = "PhotoStudio";

    // 创建一行选择器：标签 + ◄ + 名称 + ►。左右切换按钮使用 PhotoConfigPanelItemSwitchButton 预制体。
    // 标签接入多语言；切换按钮点击事件不在此绑定（统一在 Awake 中 AddListener）。
    TextMeshProUGUI CreateSelectorRow(RectTransform parent, float y, string labelZh, string labelKey, out Button prevBtn, out Button nextBtn, out LocalizeStringEvent nameLocalize)
    {
        TextMeshProUGUI labelText = UICreateTool.CreateText(labelKey + "_Label", parent, labelZh, 24f);
        labelText.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(labelText.rectTransform, new Vector2(30f, y), new Vector2(150f, 48f));
        LocTool.AttachText(labelText, PhotoStudioTable, labelKey, labelZh);

        float centerY = y - 24f;
        prevBtn = CreateSwitchButton(parent, labelKey + "_Prev", new Vector2(202f, centerY), true);   // 左按钮 Rotation z=180

        Image valueBg = UICreateTool.CreateImage(labelKey + "_ValueBg", parent, new Color(0.95f, 0.97f, 0.98f, 1f));
        UIAnchorTool.TopLeft(valueBg.rectTransform, new Vector2(228f, y), new Vector2(190f, 48f));
        TextMeshProUGUI valueText = UICreateTool.CreateText("Value", valueBg.rectTransform, "-", 22f);
        UIAnchorTool.Stretch(valueText.rectTransform);
        valueText.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        // 名称项走多语言：挂动态 LocalizeStringEvent，运行时按当前项 key 切换（key 由表格导入）
        nameLocalize = LocTool.AttachDynamicText(valueText);

        nextBtn = CreateSwitchButton(parent, labelKey + "_Next", new Vector2(444f, centerY), false);

        return valueText;
    }

    // 实例化切换按钮预制体并定位；flip=true 时 Rotation z=180（用于左侧按钮）
    Button CreateSwitchButton(RectTransform parent, string name, Vector2 centerPos, bool flip)
    {
        GameObject go = InstantiatePrefab(GuidSwitchButton, parent);
        go.name = name;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(40f, 40f);
        rt.anchoredPosition = centerPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, flip ? 180f : 0f);
        return go.GetComponent<Button>();
    }

    static GameObject InstantiatePrefab(string guid, Transform parent)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if(string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[PhotoConfigPanel] 未找到预制体 guid={guid}");
            return null;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab == null)
            return null;
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(inst, "Create Config Panel UI");
        return inst;
    }
#endif
}

// 拍照配置结果
public struct PhotoSceneConfigInfo
{
    public long bgId;
    public long lightingId;
    public long postureId;
    public long clothesId;
    public Sprite characterSprite;
}
