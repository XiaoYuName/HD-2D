using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class DramaLogUI : UIBase
{
    private ScrollRect ScrollRect;
    private CustomButton CloseButton;
    private List<LogItemUI> logItemUIs = new List<LogItemUI>();
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        ScrollRect = Get<ScrollRect>("UIMask/RawImage/Scroll View");
        CloseButton = Get<CustomButton>("UIMask/RawImage/Close");
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

    public void SetDates(List<DialogueData> dates)
    {
        logItemUIs = new List<LogItemUI>();
        for (int i = 0; i < dates.Count; i++)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.LogItemUIPath);
            obj.transform.SetParent(ScrollRect.content);
            obj.transform.localScale = Vector3.one;
            
            var slot = obj.GetComponent<LogItemUI>();
            slot.Init();
            bool isShowName = dates[i].SpeakerId > 0;
            if (isShowName)
            {
                NpcData npcData = LubanManager.Instance.TbNpcData.Get(dates[i].SpeakerId);
                slot.SetData(true,npcData.Name,dates[i].DlgText);
            }
            
            logItemUIs.Add(slot);
        }
      
    }
}
