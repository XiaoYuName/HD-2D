using System;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using XFramework;

public partial class TalkContentController : UIBase
{
    private ETalkFrame eTalkFarme;
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }
    
    public void SetFrame(ETalkFrame frame)
    {
        eTalkFarme = frame;
        normal.gameObject.SetActive(frame == ETalkFrame.Normal);
        hCG.gameObject.SetActive(frame == ETalkFrame.HCG);
    }

    public async UniTask ShowText(string text)
    {
        switch (eTalkFarme)
        {
            case ETalkFrame.Normal:
                talkNormalContext.ShowText(text);
                await UniTask.WaitUntil(() => talkNormalContext.IsShowingText);
                break;
            case ETalkFrame.HCG:
                talkHCGContext.ShowText(text);
                await UniTask.WaitUntil(() => talkNormalContext.IsShowingText);
                break;
        }
    }
}
