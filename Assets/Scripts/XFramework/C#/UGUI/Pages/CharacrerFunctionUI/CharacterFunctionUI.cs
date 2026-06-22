using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;
using Random = UnityEngine.Random;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonContent;
    private RectTransform optionCubismContent;

    private List<CustomButton> optionButtons;
    private CubismCharacterController cubismController;
    
    private CharacterData characterData;
    private ShowingData showingData;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        optionButtonContent = Get<RectTransform>("UIMask/OptionButtons");
        optionCubismContent = Get<RectTransform>("UIMask/CubismCharacterController");
    }

    public void SetData(CharacterData characterData, ShowingData showingData)
    {
        this.characterData = characterData;
        this.showingData = showingData;
        optionButtons = new List<CustomButton>();
        foreach (FunctionType functionType in Enum.GetValues(typeof(FunctionType)))
        {
            if (showingData.Functions.HasFlag(functionType))
            {
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.OptionCustomButtonPath);
                obj.transform.SetParent(optionButtonContent);
                obj.transform.localScale = Vector3.one;
                
                var btn  = obj.GetComponent<CustomButton>();
                switch (functionType)
                {
                    case FunctionType.Dialogue:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "CasualChat",
                        });
                        break;
                    case FunctionType.Dating:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Dating",
                        });
                        break;
                    case FunctionType.GiftGiving:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Gift",
                        });
                        break;
                    case FunctionType.CasinoGame:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "CasinoGame",
                        });
                        break;
                    case FunctionType.Kitchen:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Kitchen",
                        });
                        break;
                    case FunctionType.Factory:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Factory",
                        });
                        break;
                    case FunctionType.CasinoGame_1:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "CasinoGame_1",
                        });
                        break;
                    case FunctionType.Photo:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Photo",
                        });
                        break;
                }
                Bind(btn, () =>
                {
                    SelectedFunction(functionType);
                },"");
                optionButtons.Add(btn);
            }
        }

        var cubism = AssetsManager.Instance.Instantiate(characterData.CubismPrefab);
        cubism.transform.SetParent(optionCubismContent);
        cubism.transform.localPosition = Vector3.zero;
        cubismController = cubism.GetComponent<CubismCharacterController>();
        cubismController.Init();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        PlayerInputManager.Instance.OnRightClick += Close;
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        PlayerInputManager.Instance.OnRightClick -= Close;
        for (int i = 0; i < optionButtons.Count; i++)
        {
            AssetsManager.Instance.FreeGameObject(optionButtons[i].gameObject);
        }
        AssetsManager.Instance.FreeGameObject(cubismController.gameObject);
    }

    private void SelectedFunction(FunctionType functionType)
    {
        switch (functionType)
        {
            case FunctionType.Dialogue:
                var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
                if (dramaUI != null)
                {
                    dramaUI.StartDrama(showingData.NormalDramaData[Random.Range(0,showingData.NormalDramaData.Count)]);
                }
                break;
            case FunctionType.Dating:
                var setDatingUI = UISystem.Instance.OpenUI<SetDatingTargetUI>("SetDatingTargetUI");
                if (setDatingUI != null)
                {
                    setDatingUI.SetData(characterData, showingData);
                }

                break;
            case FunctionType.GiftGiving:
                break;
            case FunctionType.CasinoGame:
                var ui = UISystem.Instance.OpenUI<CasinoGameEnterPanel>("CasinoGameEnterPanel");
                ui.ShowData(1);
                break;
            case FunctionType.Kitchen:
                UISystem.Instance.OpenUI<MiniGame1KitchenManager>("KitchenPanel");
                break;
            case FunctionType.Factory:
                UISystem.Instance.OpenUI("FactoryMainPanel");
                break;
            case FunctionType.CasinoGame_1:
                var casinoGameEnterPanel = UISystem.Instance.OpenUI<CasinoGameEnterPanel>("CasinoGameEnterPanel");
                casinoGameEnterPanel.ShowData(2);
                break;
            case FunctionType.Photo:
                UISystem.Instance.OpenUI("PhotoGamePanel");
                break;
            
        }
        
        Close();
    }
}
