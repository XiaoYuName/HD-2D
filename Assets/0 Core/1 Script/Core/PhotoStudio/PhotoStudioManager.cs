using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;
// 流程：
//   None     ：全部面板关闭
//   Config   ：PhotoConfigPanel 配置四项，点击开始 / 倒计时结束 → Game
//   Game     ：PhotoStudioFocusGame 对焦小游戏，30s 结束或对焦满分 → Capture
//   Capture  ：EndPhotoPanel 闪白模拟拍照、展示照片与质量档位，按空格/点击 → Result
//   Result   ：PhotoStudioResultPanel 结算，再来一局 → Config / 返回 → None
public sealed class PhotoStudioManager : UIBase
{
    static PhotoStudioManager st;
    public static PhotoStudioManager St => st != null ? st : st = FindAnyObjectByType<PhotoStudioManager>();

    [Header("配置")]
    [SerializeField] PhotoStudioGameConfig config;

    [Header("面板引用")]
    [SerializeField] PhotoConfigPanel configPanel;
    [SerializeField] PhotoStudioFocusGame focusGame;
    [SerializeField] EndPhotoPanel endPhotoPanel;
    [SerializeField] PhotoStudioResultPanel resultPanel;

    [Header("运行时状态")]
    [SerializeField, ReadOnly] State curState;

    [SerializeField] PhotoSceneConfigInfo curConfig;
    [SerializeField] PhotoStudioPhotoResult curResult;
    bool subscribed;

    public State CurState => curState;
    public PhotoStudioGameConfig Config => config;
    public PhotoSceneConfigInfo CurConfig => curConfig;

    public override void Init()
    {
        st = this;
    }
    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    protected override void OnDestroy()
    {
        Unsubscribe();
        if(st == this)
            st = null;
    }

    void Subscribe()
    {
        if(subscribed)
            return;

        subscribed = true;
        configPanel.OnStartGame += OnConfigStartGame;
        configPanel.OnClose += OnConfigClose;
        focusGame.OnEnd += OnFocusEnd;
        endPhotoPanel.OnClose += OnEndPhotoClosed;
        resultPanel.OnReplay += OnResultReplay;
        resultPanel.OnReturn += OnResultReturn;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;

        subscribed = false;
        configPanel.OnStartGame -= OnConfigStartGame;
        configPanel.OnClose -= OnConfigClose;
        focusGame.OnEnd -= OnFocusEnd;
        endPhotoPanel.OnClose -= OnEndPhotoClosed;
        resultPanel.OnReplay -= OnResultReplay;
        resultPanel.OnReturn -= OnResultReturn;
    }

    // 外部入口：打开拍摄小游戏（如玩家点击场景中的相机）
    [Button("开始拍摄")]
    public void StartPhotoStudio()
    {
        SetState(State.Config);
    }

    // 关闭整套流程
    [Button("退出拍摄")]
    public void Exit()
    {
        SetState(State.None);
    }

    #region 面板回调
    // 配置完成（点击开始或倒计时结束）：记录配置，进入对焦小游戏
    void OnConfigStartGame(PhotoSceneConfigInfo result)
    {
        curConfig = result;
        SetState(State.Game);
    }

    // 配置面板关闭：退出
    void OnConfigClose()
    {
        SetState(State.None);
    }

    // 对焦小游戏结束（倒计时结束或对焦满分）：计算结果，进入拍照结算（闪白）
    void OnFocusEnd(bool reachedTarget, int focusScore)
    {
        curResult = BuildResult(curConfig, focusScore);
        SetState(State.Capture);
    }

    // 拍照结算面板关闭（玩家按空格/点击）：进入结算面板
    void OnEndPhotoClosed()
    {
        SetState(State.Result);
    }

    // 结算面板：再来一局（重新配置）
    void OnResultReplay()
    {
        SetState(State.Config);
    }

    // 结算面板：返回
    void OnResultReturn()
    {
        SetState(State.None);
    }
    #endregion

    #region 评分
    // 按配置与对焦积分计算一张照片的完整结算结果
    PhotoStudioPhotoResult BuildResult(PhotoSceneConfigInfo cfg, int focusScore)
    {
        int adaptation = config.GetAdaptationScore(cfg.bgId, cfg.clothesId, cfg.postureId, cfg.lightingId);
        AdaptationLevelConfig levelCfg = config.GetAdaptationLevel(adaptation);
        int quality = config.GetQualityScore(adaptation, focusScore);
        PhotoQualityTierConfig tierCfg = config.GetQualityTier(quality);

        return new PhotoStudioPhotoResult
        {
            config = cfg,
            characterSprite = cfg.characterSprite,
            focusScore = focusScore,
            adaptationScore = adaptation,
            adaptationLevel = levelCfg != null ? levelCfg.level : AdaptationLevel.Bad,
            adaptationLevelConfig = levelCfg,
            qualityScore = quality,
            qualityTier = tierCfg != null ? tierCfg.tier : PhotoQualityTier.Bad,
            qualityTierConfig = tierCfg,
        };
    }
    #endregion

    #region 状态机
    [Button]
    void SetState(State next)
    {
        ExitState(curState);
        curState = next;
        EnterState(next);
    }

    void EnterState(State s)
    {
        GameObjectTool.SetActive(gameObject, true);
        switch(s)
        {
            case State.None:
                GameObjectTool.SetActive(gameObject, false);
                GameObjectTool.SetActive(configPanel, false);
                GameObjectTool.SetActive(focusGame, false);
                GameObjectTool.SetActive(endPhotoPanel, false);
                GameObjectTool.SetActive(resultPanel, false);
                break;

            case State.Config:
                // 配置面板 OnEnable 内部会自动 OpenConfig（重置选项、刷新预览、开始倒计时）
                GameObjectTool.SetActive(configPanel, true);
                break;

            case State.Game:
                focusGame.StartGame();
                break;

            case State.Capture:
                endPhotoPanel.Init(curResult); 
                break;

            case State.Result:
                resultPanel.Show(curResult); 
                break;
        }
    }

    void ExitState(State s)
    {
        switch(s)
        {
            case State.Config:
                GameObjectTool.SetActive(configPanel, false);
                break;
            case State.Game:
                // FocusGame 结束时会自行 SetActive(false)，这里兜底
                GameObjectTool.SetActive(focusGame, false);
                break;
            case State.Capture:
                GameObjectTool.SetActive(endPhotoPanel, false);
                break;
            case State.Result:
                GameObjectTool.SetActive(resultPanel, false);
                break;
        }
    }

    #endregion

    public enum State
    {
        None,
        Config,
        Game,
        Capture,
        Result
    }
}

// 一张照片的完整结算结果（配置 + 对焦积分 → 适配度 / 质量分 / 档位）
public struct PhotoStudioPhotoResult
{
    public PhotoSceneConfigInfo config;
    public Sprite characterSprite;
    public int focusScore;                              // 对焦积分
    public int adaptationScore;                         // 适配度
    public AdaptationLevel adaptationLevel;             // 适配度档位
    public AdaptationLevelConfig adaptationLevelConfig; // 适配度档位配置（名称key等）
    public int qualityScore;                            // 照片质量分数
    public PhotoQualityTier qualityTier;                // 照片质量档位
    public PhotoQualityTierConfig qualityTierConfig;    // 质量档位配置（名称/点评/评价图/奖励）
}
