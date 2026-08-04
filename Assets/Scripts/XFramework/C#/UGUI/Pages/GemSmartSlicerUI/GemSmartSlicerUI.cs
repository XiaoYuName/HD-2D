using UnityEngine;
using XFramework;

public partial class GemSmartSlicerUI : UIBase
{
    private ClothingBag clothingBag;
    private ClothingData clothingData;
    private UIBackground Background;

    // 「切割石头」按钮，AutoBind 收不到，Init 里手动取
    private UnityEngine.UI.Button btnZhizuo;

    // 关闭时要退订，避免小游戏场景卸载后事件还挂着
    private bool subscribed;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        gameInfoUI.Init();
        Bind(btnTuichu,Close,"");

        // 「切割石头」按钮挂的是原生 Button 而不是 CustomButton，AutoBind 没收进去，手动绑。
        // 自由切割没有「切完 N 条边」这种自然终点，玩家不点它就永远不会走结算。
        btnZhizuo = Get<UnityEngine.UI.Button>("UIMask/SmartSlicerButton/anniu/btn_zhizuo");
        if (btnZhizuo != null)
        {
            Bind(btnZhizuo, FinishGame, "");
        }
    }

    /// <summary>玩家交卷：按当前切出来的形状立刻结算。</summary>
    private void FinishGame()
    {
        if (!GameSmartController.IsInitialized)
        {
            Debug.LogError("[GemCut] 小游戏场景还没加载完，无法结算。");
            return;
        }

        GameSmartController.Instance.Finish();
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
        Debug.Log($"[GemCut] 本局得分 {result.score}，胜利 = {result.win}，" +
                  $"轮廓内损伤 {result.targetDamage:P1}，切坏 = {result.failed}");

        if (result.win)
        {
            // 通关了就不再接结算回调，避免完成窗还开着时又被触发
            Unsubscribe();
            UIUtility.PopClothingMinGameComplete(Close);
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

    /// <summary>
    /// 宝石形状取自服装配置，所以要传服装；小游戏已经不参与解锁，不需要角色数据。
    /// </summary>
    public void SetData(ClothingBag clothingBag)
    {
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
