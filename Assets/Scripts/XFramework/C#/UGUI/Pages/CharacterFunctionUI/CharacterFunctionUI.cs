using System;
using System.Collections.Generic;
using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonRect;
    private RectTransform optionCharacterRect;
    private Image characterPortraitImg;
    
    private CharacterData characterData;
    private List<CustomButton> optionButtons;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        optionButtonRect = Get<RectTransform>("UIMask/OptionButtons");
        optionCharacterRect = Get<RectTransform>("UIMask/CubismCharacterController");
        characterPortraitImg = Get<Image>("UIMask/CubismCharacterController/llustration");
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var optionButton in optionButtons)
        {
            AssetsManager.Instance.FreeGameObject(optionButton.gameObject);
        }
    }


    public void SetData(CharacterData characterData,ShowingData showingData)
    {
        this.characterData = characterData;
        characterPortraitImg.sprite = characterData.DialogueTexture;
        characterPortraitImg.SetNativeSize();
        foreach (var type in Enum.GetValues(typeof(FunctionType)))
        {
            if (showingData.FunctionGroup.HasFlag((FunctionType)type))
            {
                //生成对应角色功能按钮
               var obj = AssetsManager.Instance.Instantiate(AssetKeys.OptionCustomButtonPath);
               obj.transform.SetParent(optionButtonRect);
               obj.transform.localScale = Vector3.one;
               obj.transform.localPosition = Vector3.zero;
               var btn =  obj.GetComponent<CustomButton>();
               btn.onClick.RemoveAllListeners();
               btn.onClick.AddListener(() =>
               {
                   //OptionMovToDialogue(data.NextDlgId);
               });
               btn.SetLabel(new LocalSelectedData()
               {
                   Table = "EnumsText", 
                   Value = type.ToString()
               });
               optionButtons.Add(btn);
            }
        }
    }

    private void OnOnClickFunction(FunctionType type)
    {
        CharacterManager.Instance.Execute(type,characterData);
    }
}
