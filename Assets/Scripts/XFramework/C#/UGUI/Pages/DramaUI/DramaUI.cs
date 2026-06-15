using System;
using System.Collections.Generic;
using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class DramaUI : UIBase
{
    private DramaDialogueNameSlot _dialogueNameSlot;
    /// <summary>
    /// 打字机对象
    /// </summary>
    private TypewriterComponent typewriter;
    private LocalizeStringEvent typewriterStringEvent;
    /// <summary>
    /// 当前播放的剧情
    /// </summary>
    private DramaData dramaData;
    /// <summary>
    /// 当前执行的剧情命令
    /// </summary>
    public DramaCommand CurrentCommand { get; private set; }
    public int CurrentCommandIndex { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _dialogueNameSlot = Get<DramaDialogueNameSlot>("UIMask/NameFarme/DramaDialogueNameSlot");
        _dialogueNameSlot.Init();
        typewriter = Get<TypewriterComponent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
        typewriterStringEvent = Get<LocalizeStringEvent>("UIMask/DramaFarme/DialogueFarme/Typewrite");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        AGVInputManager.Instance.OnClick += MouseClick;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        AGVInputManager.Instance.OnClick -= MouseClick;
    }

    public void StartDrama(DramaData dramaData)
    {
        this.dramaData = dramaData;
        CurrentCommandIndex = 0;
        CurrentCommand = this.dramaData.Commands[CurrentCommandIndex];
        CurrentCommand.Init(this);
        CurrentCommand.Enter();
    }

    #region DialogueCommand

    public void ShowDialogue(LocalSelectedData content)
    {
        _dialogueNameSlot.gameObject.SetActive(false);
        typewriterStringEvent.StringReference.SetReference(content.Table,content.Value);
        typewriterStringEvent.StringReference.RefreshString();
    }

    public void ShowDialogue(LocalSelectedData name,DialogueDirection direction,LocalSelectedData content)
    {
        _dialogueNameSlot.gameObject.SetActive(true);
        _dialogueNameSlot.ChangeDirection(direction);
        _dialogueNameSlot.SetContent(name);
        typewriterStringEvent.StringReference.SetReference(content.Table,content.Value);
        typewriterStringEvent.StringReference.RefreshString();
    }

    public void SkipDialogue()
    {
        if (typewriter.IsShowingText)
        {
            typewriter.SkipTypewriter();
        }
        else
        {
            NextDrama();
        }
    }

    #endregion

    #region OptionsCommand

    private const string OptionButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DramaUI/OptionCostomButton.prefab";
    private List<CustomButton> _optionButtons = new List<CustomButton>();

    public void ShowOptions(List<DramaOptionsData> options, Action<DramaOptionsData> selectedCallback)
    {
        _optionButtons = new List<CustomButton>();
        for (int i = 0; i < options.Count; i++)
        {
            var obj = AssetsManager.Instance.LoadAssets<CustomButton>(OptionButtonPath);
            obj.transform.localScale = Vector3.one;
            var btn =obj.GetComponent<CustomButton>();
            btn.onClick.RemoveAllListeners();
            var index = i;
            btn.onClick.AddListener(() => {selectedCallback?.Invoke(options[index]);});
            btn.SetLabel(options[i].LocalSelectedData);
            _optionButtons.Add(btn);
        }
    }

    #endregion
    
    public void MouseClick()
    {
        if (CurrentCommand != null)
        {
            CurrentCommand.Exit();
            return;
        }
    }
    private void NextDrama()
    {
        CurrentCommandIndex++;
        if(CurrentCommandIndex < dramaData.Commands.Count)
        {
            CurrentCommand = dramaData.Commands[CurrentCommandIndex];
            CurrentCommand.Init(this);
            CurrentCommand.Enter();
        }
        else
        {
            Close();
        }
    }

    private void Update()
    {
        CurrentCommand?.Update();
    }
}
