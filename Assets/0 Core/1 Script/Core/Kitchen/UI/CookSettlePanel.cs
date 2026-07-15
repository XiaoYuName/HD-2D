using UnityEngine;
using XFramework;
using System;
using UnityEngine.UI;

public class CookSettlePanel : UIBase
{
    [SerializeField] MakeFoodResTip makeFoodResTip;
    [SerializeField] Button returnButton;

    Data curData;

    /// <summary>结算展示数据：调用方（MiniGame1UI）算好烹饪结果后传入，本面板只负责呈现。</summary>
    public class Data
    {
        public MiniGameCookResult Result;
        /// <summary>点击「返回」回调；为空时仅关闭本面板。</summary>
        public Action OnBack;
    }

    public override void Init()
    {
        returnButton.onClick.AddListener(OnReturnButton);
    }

    /// <summary>填充并刷新结算面板，需在 OpenUI 之后调用。</summary>
    public void Show(Data data)
    {
        curData = data;
        makeFoodResTip.ShowTip(data.Result.IsSuccess ? LocVarSet.MiniGame1CookGame.MakeFoodSuccess : LocVarSet.MiniGame1CookGame.MakeFoodFail, data.Result.ResultItem);
    }

    void OnReturnButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }
}
