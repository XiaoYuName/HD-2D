using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonContent;
    private RectTransform optionCubismContent;

    private const string optionButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DramaUI/OptionCustomButton.prefab";

    private List<CustomButton> optionButtons;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        optionButtonContent = Get<RectTransform>("");
        optionCubismContent = Get<RectTransform>("UIMask/CubismCharacterController");
    }

    public void SetData(CharacterData characterData, ShowingData showingData)
    {
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
    }

    private void SelectedFunction(FunctionType functionType)
    {
        Debug.Log("选择了 :" + functionType);
    }
}
