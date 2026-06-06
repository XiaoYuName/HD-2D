using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XFramework;

public class MainUI : UIBase
{
    private AGVButton GameMapButton;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        GameMapButton = Get<AGVButton>("UIMask/Panel/GameMapButton");
        BindAGVClick(GameMapButton,LoadGameMap,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        BindEvents();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        UnBindEvents();
    }

    #region Bind

    private bool isBind;

    private void BindEvents()
    {
        if (!isBind)
        {
            isBind = true;
            GameDataManager.Instance.BindUserChange(UpdateUserUI);
        }
    }

    private void UnBindEvents()
    {
        if (isBind)
        {
            GameDataManager.Instance.UnBindUserChange(UpdateUserUI);
            isBind = false;
        }
    }

    #endregion


    private void UpdateUserUI(User user)
    {
        
    }

    public void LoadGameMap()
    {
        // var data = GameDataManager.Instance.MinGameSceneData.GetDataByID(GameDataManager.Instance.CurrentUser.minSceneID);
        // UISystem.Instance.CloseUI(data.page_id);
        // AssetsManager.Instance.ULoadSceneAsync(data.scenePath);
        // AssetsManager.Instance.LoadScene("Assets/AddressableAssets/Remote/Scenes/WordMap.unity",LoadSceneMode.Additive);
    }
}
