using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Services;
using Febucci.UI;
using UnityEngine;
using XFramework;

public partial class TalkContentController : UIBase
{
    private ETalkFrame eTalkFarme;
    private TypewriterByCharacter normalTypewrite;
    private TypewriterByCharacter hCGTypewrite;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        normalTypewrite = talkNormalContext.GetComponent<TypewriterByCharacter>();
        hCGTypewrite = talkHCGContext.GetComponent<TypewriterByCharacter>();
    }
    
    public void SetFrame(ETalkFrame frame)
    {
        eTalkFarme = frame;
        normal.gameObject.SetActive(frame == ETalkFrame.Normal);
        hCG.gameObject.SetActive(frame == ETalkFrame.HCG);
    }

    public async UniTask ShowText(DialogueLine line,CancellationToken ct)
    {
        switch (eTalkFarme)
        {
            case ETalkFrame.Normal:
                talkNormalContext.SetText(line.TextRef.Table,line.TextRef.Key);
                await UniTask.WaitWhile(() => normalTypewrite.isShowingText, cancellationToken: ct);
                break;
            case ETalkFrame.HCG:
                talkHCGContext.SetText(line.TextRef.Table,line.TextRef.Key);
                await UniTask.WaitWhile(() => hCGTypewrite.isShowingText, cancellationToken: ct);
                break;
        }
    }
}
