using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonRect;
    private Image characterPortraitImg;
    
    private CharacterData characterData;
    private NpcData npcData;
    private List<CustomButton> optionButtons;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        optionButtonRect = Get<RectTransform>("UIMask/OptionButtons");
        characterPortraitImg = Get<Image>("UIMask/CubismCharacterController/llustration");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
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

        if (npcData != null)
        {
            AssetsManager.Instance.FreeAsset($"{AssetsPaths.DialogueTexturePath}{npcData.MiniImg}.png");
        }
    }


    public void SetData(NpcData data)
    {
        this.npcData = data;
        characterPortraitImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>($"{AssetsPaths.DialogueTexturePath}{npcData.MiniImg}.png");
        characterPortraitImg.SetNativeSize();
        characterPortraitImg.gameObject.SetActive(true);
        optionButtons = new List<CustomButton>();
        foreach (FunctionGroup type in Enum.GetValues(typeof(FunctionGroup)))
        {
            if(type == FunctionGroup.Node)continue;
            if (npcData.FunctionType.HasFlag(type))
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
                   OnOnClickFunction(type);
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

    private void OnOnClickFunction(FunctionGroup type)
    {
        Close();
        CharacterManager.Instance.Execute(type,npcData);
    }
}
