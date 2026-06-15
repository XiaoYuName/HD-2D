using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class DramaLogUI : UIBase
{
    private const string LogItemUIPath = "Assets/AddressableAssets/Remote/Prefabs/UGUI/DramaLogUI/LogItemUI.prefab";
    private ScrollRect ScrollRect;
    private CustomButton CloseButton;
    private List<LogItemUI> logItemUIs = new List<LogItemUI>();
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        ScrollRect = Get<ScrollRect>("UIMask/RawImage/Scroll View");
        Bind(CloseButton,Close,"");
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var t in logItemUIs)
        {
            AssetsManager.Instance.FreeGameObject(t.gameObject);
        }
    }

    public void SetDates(List<DialogueCommand> dates)
    {
        logItemUIs = new List<LogItemUI>();
        for (int i = 0; i < dates.Count; i++)
        {
            var obj = AssetsManager.Instance.Instantiate(LogItemUIPath);
            obj.transform.SetParent(ScrollRect.content);
            obj.transform.localScale = Vector3.one;
            
            var slot = obj.GetComponent<LogItemUI>();
            slot.SetData(dates[i].dialogueName,dates[i].dialogueText);
            logItemUIs.Add(slot);
        }
      
    }
}
