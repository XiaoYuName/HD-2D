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
    #region 文本
    private DramaDialogueNameSlot _dialogueNameSlot;
    /// <summary>
    /// 打字机对象
    /// </summary>
    private TypewriterComponent typewriter;
    
    private LocalizeStringEvent typewriterStringEvent;
    

    #endregion

    #region 选项

    private RectTransform _optionButtonsParent;
    private List<CustomButton> _optionButtons = new List<CustomButton>();
    #endregion

    #region 立绘

    private RectTransform IllustrationRect;//立绘根节点
    private RectTransform LeftDirectionPoint;
    private RectTransform RightDirectionPoint;
    private RectTransform CenterDirectionPoint;
    private RectTransform SpritePoolRect;

    /// <summary>
    /// 立绘控制器
    /// </summary>
    private List<CharacterPortraitController> PortraitControllers = new List<CharacterPortraitController>();
    

    #endregion


    #region 数据

    
    private CancellationTokenSource autoTokenSource;
    private DialogueData currentDialogueData;

    #endregion

    

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _dialogueNameSlot = Get<DramaDialogueNameSlot>("UIMask/NameFarme/DramaDialogueNameSlot");
        _dialogueNameSlot.Init();
        typewriter = Get<TypewriterComponent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
        typewriterStringEvent = Get<LocalizeStringEvent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
        _optionButtonsParent = Get<RectTransform>("UIMask/OptionFarme");
        IllustrationRect = Get<RectTransform>("UIMask/IllustrationFarme");
        LeftDirectionPoint = Get<RectTransform>("UIMask/IllustrationFarme/LeftDirectionPointFarme");
        RightDirectionPoint = Get<RectTransform>("UIMask/IllustrationFarme/RightDirectionPointFarme");
        CenterDirectionPoint = Get<RectTransform>("UIMask/IllustrationFarme/CenterDirectionPointFarme");
        SpritePoolRect = Get<RectTransform>("UIMask/IllustrationFarme/SpritePoolFarme");
        
        typewriter.onTextShowed.RemoveAllListeners();
        typewriter.onTextShowed.AddListener(() =>
        {
            if (DramaManager.Instance.isAutoDrama && currentDialogueData.IsSkippable)
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

        foreach (var controller in PortraitControllers)
        {
            controller.Release();
            AssetsManager.Instance.FreeGameObject(controller.gameObject);
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
            _dialogueNameSlot.ChangeDirection(dialogueData.SpritePos);
            var npcData = DramaManager.Instance.GetNpcData(dialogueData.SpeakerId);
            _dialogueNameSlot.SetContent(npcData.Name.Table,npcData.Name.Value);

            if (!string.IsNullOrEmpty(npcData.MiniImg))
            {
                if (PortraitControllers.Count > 0)
                {
                    int index = PortraitControllers.Count - 1;
                    CharacterPortraitController lastController = PortraitControllers[index];
                    switch (dialogueData.PrevSpriteHandle)
                    {
                        case PrevSpriteHandleType.DEL:
                            lastController.Release();
                            AssetsManager.Instance.FreeGameObject(lastController.gameObject);
                            PortraitControllers.RemoveAt(index);
                            break;
                        case PrevSpriteHandleType.MASK:
                            lastController.SetMask();
                            break;
                        case PrevSpriteHandleType.OVERRIDE:
                            break;
                    }
                }
                
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.LlustrationPath);
                obj.transform.SetParent(SpritePoolRect);
                obj.transform.localScale = Vector3.one;
                var rect = obj.GetComponent<RectTransform>();
                switch (dialogueData.SpritePos)
                {
                    case LlustrationDirection.Left:
                        rect.anchorMin = LeftDirectionPoint.anchorMin;
                        rect.anchorMax = LeftDirectionPoint.anchorMax;
                        rect.pivot = LeftDirectionPoint.pivot;
                        rect.anchoredPosition = LeftDirectionPoint.anchoredPosition;
                        break;
                    case LlustrationDirection.Crent:
                        rect.anchorMin = CenterDirectionPoint.anchorMin;
                        rect.anchorMax = CenterDirectionPoint.anchorMax;
                        rect.pivot = CenterDirectionPoint.pivot;
                        rect.anchoredPosition = CenterDirectionPoint.anchoredPosition;
                        break;
                    case LlustrationDirection.Right:
                        rect.anchorMin = RightDirectionPoint.anchorMin;
                        rect.anchorMax = RightDirectionPoint.anchorMax;
                        rect.pivot = RightDirectionPoint.pivot;
                        rect.anchoredPosition = RightDirectionPoint.anchoredPosition;
                        break;
                }
                var controller = obj.GetComponent<CharacterPortraitController>();
                controller.SetData(npcData);
                PortraitControllers.Add(controller);
            }
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

        if (currentDialogueData.SaveFlag)
        {
            SaveGameManager.Instance.Save();
        }
    }

    private void MovToDialogue(long nextDialogueID)
    {
        //关闭自动对话计时器
        StopAutoNextDialogue();

        //退出对话判断
        if (!HasNext(nextDialogueID) || !HasFrontDialogue(currentDialogueData))
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
        if (!HasOptionNext(nextDialogueID) || !HasFrontDialogue(currentDialogueData))
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

    private bool HasFrontDialogue(DialogueData dialogueData)
    {
        if (dialogueData != null)
        {
            if (dialogueData.PreReqDlgId.Count <= 0)
            {
                return true;
            }

            foreach (var ID in dialogueData.PreReqDlgId)
            {
                if (!DramaManager.Instance.HasDialogue(ID))
                {
                    return false;
                }
            }

            return true;
        }
        return false;
    }

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
