using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class DramaUI : UIBase
{
    private RectTransform DramaUIParent;
    private RectTransform NameUIParent;
    
    private DramaDialogueNameSlot _dialogueNameSlot;
    /// <summary>
    /// 打字机对象
    /// </summary>
    private TypewriterComponent typewriter;
    
    private LocalizeStringEvent typewriterStringEvent;
    private RectTransform _optionButtonsParent;
    

    private CancellationTokenSource autoTokenSource;
    private DialogueData currentDialogueData;
    private List<CustomButton> _optionButtons = new List<CustomButton>();

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        NameUIParent = Get<RectTransform>("UIMask/NameFarme");
        DramaUIParent = Get<RectTransform>("UIMask/DramaFarme");
        _dialogueNameSlot = Get<DramaDialogueNameSlot>("UIMask/NameFarme/DramaDialogueNameSlot");
        _dialogueNameSlot.Init();
        typewriter = Get<TypewriterComponent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
        typewriterStringEvent = Get<LocalizeStringEvent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
        _optionButtonsParent = Get<RectTransform>("UIMask/OptionFarme");
        
        typewriter.onTextShowed.RemoveAllListeners();
        typewriter.onTextShowed.AddListener(() =>
        {
            if (DramaManager.Instance.isAutoDrama)
            {
                WaitAutoNextDialogue(currentDialogueData.NextDlgId).Forget();
            }
        });
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        PlayerInputManager.Instance.OnClick += MouseClick;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        PlayerInputManager.Instance.OnClick -= MouseClick;
        foreach (var btn in _optionButtons)
        {
            AssetsManager.Instance.FreeGameObject(btn.gameObject);
        }
    }

    public void StartDrama(long startDialogueID)
    {
        DialogueData dialogueData = LubanManager.Instance.TbDialogueData.Get(startDialogueID);
        Dialogue(dialogueData);
    }

    public void Dialogue(DialogueData dialogueData)
    {
        currentDialogueData = dialogueData;
        
        typewriterStringEvent.SetText(dialogueData.DlgText.Table,dialogueData.DlgText.Value);
        if (dialogueData.SpeakerId > 0)
        {
            _dialogueNameSlot.gameObject.SetActive(true);
            var npcData = LubanManager.Instance.TbNpcData.Get(dialogueData.SpeakerId);
            _dialogueNameSlot.SetContent(npcData.Name.Table,npcData.Name.Value);
        }
        else
        {
            _dialogueNameSlot.gameObject.SetActive(false);
        }

        if (dialogueData.OptGrpId.Count > 0)
        {
            _optionButtonsParent.gameObject.SetActive(true);
            foreach (var id in dialogueData.OptGrpId)
            {
                DialogueData data = LubanManager.Instance.TbDialogueData.Get(id);
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.OptionCustomButtonPath);
                obj.transform.SetParent(_optionButtonsParent);
                obj.transform.localScale = Vector3.one;
                var btn =obj.GetComponent<CustomButton>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    OptionMovToDialogue(data.NextDlgId);
                });
                btn.SetLabel(data.DlgText);
                _optionButtons.Add(btn);
            }
        }
        else
        {
            foreach (var btn in _optionButtons)
            {
                AssetsManager.Instance.FreeGameObject(btn.gameObject);
            }
            _optionButtonsParent.gameObject.SetActive(false);
        }

    }

    private void MovToDialogue(long nextDialogueID)
    {
        //关闭自动对话计时器
        StopAutoNextDialogue();

        //退出对话判断
        if (!HasNext(nextDialogueID))
        {
            Close();
            return;
        }
        
        if (currentDialogueData.OptGrpId.Count <= 0)
        {
            DialogueData dialogueData = LubanManager.Instance.TbDialogueData.Get(nextDialogueID);
            Dialogue(dialogueData);
        }

        
    }

    private void OptionMovToDialogue(long nextDialogueID)
    {
        //关闭自动对话计时器
        StopAutoNextDialogue();

        //退出对话判断
        if (!HasOptionNext(nextDialogueID))
        {
            Close();
            return;
        }
        
        DialogueData dialogueData = LubanManager.Instance.TbDialogueData.Get(nextDialogueID);
        Dialogue(dialogueData);
    }

    private async UniTask WaitAutoNextDialogue(long nextDialogueID)
    {
        if (autoTokenSource != null)
        {
            autoTokenSource.Cancel();
            autoTokenSource.Dispose();
        }
        autoTokenSource = new CancellationTokenSource();
        await UniTask.Delay(TimeSpan.FromSeconds(currentDialogueData.AutoPlayDuration), cancellationToken:autoTokenSource.Token);
        MovToDialogue(nextDialogueID);
    }

    private void StopAutoNextDialogue()
    {
        if (autoTokenSource != null)
        {
            autoTokenSource.Cancel();
            autoTokenSource.Dispose();
            autoTokenSource = null;
        }
    }

    #region 判断

    /// <summary>
    /// 判断是否还有下一条数据
    /// </summary>
    /// <param name="nextDialogueID"></param>
    /// <returns></returns>
    private bool HasNext(long nextDialogueID)
    {
        if (nextDialogueID <= 0 && currentDialogueData.OptGrpId.Count <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 选项的判断是否还有下一条数据
    /// </summary>
    /// <param name="nextDialogueID"></param>
    /// <returns></returns>
    private bool HasOptionNext(long nextDialogueID)
    {
        if (nextDialogueID <= 0)
        {
            return false;
        }
        return true;
    }

    #endregion

    private void MouseClick()
    {
        if (currentDialogueData == null) return;
        if (currentDialogueData.IsSkippable)
        {
            typewriter.SkipTypewriter();
            MovToDialogue(currentDialogueData.NextDlgId);
        }
    }

}
