using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;
using Random = UnityEngine.Random;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonContent;
    private RectTransform optionCubismContent;

    private const string optionButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DramaUI/OptionCustomButton.prefab";

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
                var obj = AssetsManager.Instance.Instantiate(optionButtonPath);
                obj.transform.SetParent(optionButtonContent);
                obj.transform.localScale = Vector3.one;
                
                var btn  = obj.GetComponent<CustomButton>();
                switch (showingData.Functions)
                {
                    case FunctionType.Dialogue:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "CasualChat",
                        });
                        break;
                    case FunctionType.Task:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Task",
                        });
                        break;
                    case FunctionType.GiftGiving:
                        btn.SetLabel(new LocalSelectedData()
                        {
                            Table = "DramaOptions",
                            Value =  "Gift",
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
        AGVInputManager.Instance.OnRightClick += Close;
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        AGVInputManager.Instance.OnRightClick -= Close;
        for (int i = 0; i < optionButtons.Count; i++)
        {
            AssetsManager.Instance.FreeGameObject(optionButtons[i].gameObject);
        }
        AssetsManager.Instance.FreeGameObject(cubismController.gameObject);
    }

    private void SelectedFunction(FunctionType functionType)
    {
        Debug.Log("选择了 :" + functionType);
        switch (functionType)
        {
            case FunctionType.Dialogue:
                var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
                if (dramaUI != null)
                {
                    dramaUI.StartDrama(showingData.NormalDramaData[Random.Range(0,showingData.NormalDramaData.Count)]);
                }
                break;
            case FunctionType.Task:
                break;
            case FunctionType.GiftGiving:
                break;
        }
        
        Close();
    }
}
