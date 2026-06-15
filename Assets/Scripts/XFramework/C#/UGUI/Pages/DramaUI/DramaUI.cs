using System;
using System.Collections.Generic;
using System.Linq;
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
        CurrentCommand = this.dramaData.Commands[0];
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
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DramaUI/OptionCustomButton.prefab";
    private List<CustomButton> _optionButtons = new List<CustomButton>();
    private RectTransform _optionButtonsParent;

    public void ShowOptions(List<DramaOptionsData> options, Action<DramaOptionsData> selectedCallback)
    {
        _optionButtons = new List<CustomButton>();
        _optionButtonsParent.gameObject.SetActive(true);
        for (int i = 0; i < options.Count; i++)
        {
            var obj = AssetsManager.Instance.Instantiate(OptionButtonPath);
            obj.transform.SetParent(_optionButtonsParent);
            obj.transform.localScale = Vector3.one;
            var btn =obj.GetComponent<CustomButton>();
            btn.onClick.RemoveAllListeners();
            var index = i;
            btn.onClick.AddListener(() => {selectedCallback?.Invoke(options[index]);});
            btn.SetLabel(options[i].LocalSelectedData);
            _optionButtons.Add(btn);
        }
    }

    public void CloseOptions()
    {
        _optionButtonsParent.gameObject.SetActive(false);
        foreach (var btn in _optionButtons)
        {
            AssetsManager.Instance.FreeGameObject(btn.gameObject);
        }
        _optionButtons.Clear();
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

    public void ToDrama(int index)
    {
        if(dramaData.Commands.Any(temp=> temp.CommandIndex == index))
        {
            CurrentCommand = dramaData.Commands
                .FindLast(temp => temp.CommandIndex == index);
            CurrentCommand.Init(this);
            CurrentCommand.Enter();
        }
        else
        {
            Close();
        }
    }

    private void NextDrama()
    {
        if (dramaData.Commands.Any(temp => temp.CommandIndex == CurrentCommand.ToIndex))
        {
            
            CurrentCommand = dramaData.Commands
                .FindLast(temp => temp.CommandIndex == CurrentCommand.ToIndex);
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
