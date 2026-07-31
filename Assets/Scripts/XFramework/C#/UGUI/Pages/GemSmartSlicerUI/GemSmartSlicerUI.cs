using UnityEngine;
using XFramework;

public partial class GemSmartSlicerUI : UIBase
{
    /// <summary>及格线：切割评分到这个分数才算通关。</summary>
    private const int PassScore = 80;

    private CharacterBag  characterBag;
    private ClothingBag clothingBag;
    private ClothingData clothingData;
    private UIBackground Background;

    // 关闭时要退订，避免小游戏场景卸载后事件还挂着
    private bool subscribed;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        gameInfoUI.Init();
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameSceneManager.Instance.EnterMinGameScene(MinGameSceneType.GemSmartSlicerScene,StartGame);
        Background =  UISystem.Instance.LoadUIBackground<UIBackground>(AssetKeys.GemSmartSlicerBackgroundUIPath);
        //Cursor.visible = false;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        // 必须赶在卸载小游戏场景之前退订，之后 GameSmartController 就跟着场景一起没了
        Unsubscribe();

        base.Close();
        GameSceneManager.Instance.QuitMinGameScene();
        if (Background != null)
        {
            UISystem.Instance.ReleaseUIBackground(Background);
        }
        //Cursor.visible = true;
    }

    /// <summary>小游戏场景加载完成后的回调，开第一局。</summary>
    private void StartGame()
    {
        if (clothingData == null)
        {
            Debug.LogError("[GemCut] 没有 ClothingData，无法开始宝石切割。");
            return;
        }

        // 先订阅再发牌，否则理论上有漏掉结算回调的可能
        Subscribe();
        GameSmartController.Instance.SetData(clothingData);
    }

    private void Subscribe()
    {
        if (subscribed || !GameSmartController.IsInitialized)
        {
            return;
        }

        GameSmartController.Instance.Completed += Completed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (GameSmartController.IsInitialized)
        {
            GameSmartController.Instance.Completed -= Completed;
        }

        subscribed = false;
    }

    /// <summary>
    /// 一局切完的结算。达标就走通用的服装小游戏完成流程，
    /// 不达标弹失败窗：重试原地换一块新宝石，确认则退出玩法面板。
    /// </summary>
    private void Completed(GemCutResult result)
    {
        Debug.Log($"[GemCut] 本局得分 {result.score}（及格 {PassScore}），" +
                  $"轮廓内损伤 {result.targetDamage:P1}，切坏 = {result.failed}");

        if (result.score >= PassScore)
        {
            // 通关了就不再接结算回调，避免完成窗还开着时又被触发
            Unsubscribe();
            UIUtility.PopClothingMinGameComplete(characterBag, clothingBag,
                ClothingMinGameType.GemSmartSlicer, Close);
        }
        else
        {
            // 玩法面板不关，失败窗自带黑底遮罩挡住下面的点击，重试时窗口自己会关
            UIUtility.PopFailWindow(true, RetryGame, Close);
        }
    }

    /// <summary>重试：换一块完好的宝石重新开始，轮廓和衣服数据都不变。</summary>
    private void RetryGame()
    {
        if (!GameSmartController.IsInitialized)
        {
            Debug.LogError("[GemCut] 小游戏场景已经卸载，无法重试。");
            return;
        }

        GameSmartController.Instance.Retry();
    }

    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
        if (clothingBag != null)
        {
            clothingData = LubanManager.Instance.TbClothingData.Get(clothingBag.clothingID);
            if (clothingData != null)
            {
                gameInfoUI.SetData(clothingData);
            }
        }
    }
}
