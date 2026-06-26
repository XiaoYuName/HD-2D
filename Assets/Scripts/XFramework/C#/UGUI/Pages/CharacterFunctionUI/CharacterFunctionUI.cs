using System;
using UnityEngine;
using XFramework;

public class CharacterFunctionUI : UIBase
{
    private RectTransform optionButtonRect;
    private RectTransform optionCharacterRect;
    private CharacterPortraitController characterPortraitController;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        
    }


    public void SetData(CharacterData characterData,ShowingData showingData)
    {
       
        foreach (var type in Enum.GetValues(typeof(FunctionType)))
        {
            if (showingData.FunctionGroup.HasFlag((FunctionType)type))
            {
                //生成对应角色功能按钮
            }
        }
    }
}
