using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Coffee.UIEffects;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class ExhibitionCharacterUI : UIBase
{
    public ExhibitionState State { get; private set; } = ExhibitionState.Idle;
    private Sequence _sequence;
    private UIEffect iconUIEffect;

    public Action<ExhibitionCharacterUI> OnClick;

    /// <summary>
    /// 需求物品
    /// </summary>
    public ExhibitionGameData NeedGameData { get; private set; }

    private bool isSendData = false;

    [LabelText("展示槽位")]
    public List<FlySlot> FlySlots;

    public override void Init()
    {
        InitAutoBind();
        OnClick = null;

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        infoUI.transform.localScale = Vector3.zero;
        for (int i = 0; i < FlySlots.Count; i++)
        {
            FlySlots[i].gameObject.SetActive(false);
            FlySlots[i].transform.localScale = Vector3.zero;
            FlySlots[i].Init();
        }

        iconUIEffect = icon.GetComponent<UIEffect>();

        icon.triggers.Clear();

        EventTrigger.Entry pointerEnter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        pointerEnter.callback.AddListener(data => OnPointerEnter((PointerEventData)data));
        icon.triggers.Add(pointerEnter);

        EventTrigger.Entry pointerExit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        pointerExit.callback.AddListener(data => OnPointerExit((PointerEventData)data));
        icon.triggers.Add(pointerExit);

        EventTrigger.Entry pointerClick = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        pointerClick.callback.AddListener(data => OnPointerClick((PointerEventData)data));
        icon.triggers.Add(pointerClick);
    }

    public void SetData(ExhibitionGameData exhibitionGameData)
    {
        if (State != ExhibitionState.Idle) return;
        State = ExhibitionState.Waiting;
        isSendData = false;
        NeedGameData = exhibitionGameData;
        photograph.gameObject.SetActive(exhibitionGameData.isPhotograph);

        for (int i = 0; i < FlySlots.Count; i++)
        {
            if (i < exhibitionGameData.FactoryInfo.Count)
            {
                FlySlots[i].gameObject.SetActive(true);
                FlySlots[i].transform.DOScale(Vector3.one, 0.15f);
                FlySlots[i].SetData(ExhibitionManager.Instance.GetMappingFlyItemSlotData(exhibitionGameData.FactoryInfo[i]));
            }
            else
            {
                FlySlots[i].gameObject.SetActive(false);
            }

        }
        
        
        _sequence = DOTween.Sequence();
        _sequence.Append(exhibitionCharacterUI.DOFade(1, 0.15f));
        _sequence.AppendInterval(0.5f);
        _sequence.Append(infoUI.transform.DOScale(Vector3.one, 0.35f));
        StartCoroutine(Dwell());
        
    }

    private float currentDwellTime;
    public IEnumerator Dwell()
    {
        currentDwellTime = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        dwellSlider.minValue = 0;
        dwellSlider.maxValue = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        dwellSlider.value = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        while (currentDwellTime > 0)
        {
            if (!isSendData)
            {
                currentDwellTime -= Time.deltaTime;
            }
            dwellSlider.value = currentDwellTime;
            yield return null;
        }
        currentDwellTime = 0;
        dwellSlider.value = currentDwellTime;
        
        checkFamre.gameObject.SetActive(true);
        processIcon.transform.localScale = Vector3.zero;
        successIcon.transform.localScale = Vector3.zero;
        failIcon.transform.localScale = Vector3.zero;
        
        _sequence = DOTween.Sequence();
        exitText.gameObject.SetActive(true);
        exitText.transform.localScale = Vector3.zero;
        _sequence.Append(exitText.DOScale(Vector3.one, 0.15f));
        _sequence.Append(infoUI.transform.DOScale(Vector3.zero, 0.35f));
        _sequence.AppendInterval(0.75f);
        _sequence.Append(exhibitionCharacterUI.DOFade(0, 0.15f));
        yield return _sequence.WaitForCompletion();
        OnClick = null;
        isSendData = false;
        exitText.gameObject.SetActive(false);
        exitText.transform.localScale = Vector3.zero;
        State = ExhibitionState.Idle;
    }

    public void SendBuyItem(ExhibitionGameData exhibitionGameData)
    {
        isSendData = true;
        OnPointerExit(null);
        if (exhibitionGameData.FactoryInfo.Count == NeedGameData.FactoryInfo.Count)
        {
            foreach (var factoryMerchandiseItemInfo in NeedGameData.FactoryInfo)
            {
                if (exhibitionGameData.FactoryInfo.All(temp => temp.ID != factoryMerchandiseItemInfo.ID))
                {
                    CheckFail();
                }
            }

            CheckSuccess();
        }
        else
        {
            CheckFail();
        }
    }

    private Sequence sendSequence;
    private void CheckSuccess()
    {
        sendSequence?.Kill();
        sendSequence = DOTween.Sequence();
        checkFamre.alpha = 0;
        checkFamre.gameObject.SetActive(true);
        processIcon.transform.localScale = Vector3.zero;
        successIcon.transform.localScale = Vector3.zero;
        failIcon.transform.localScale = Vector3.zero;
        sendSequence.Append(checkFamre.DOFade(1, 0.15f));
        sendSequence.Append(processIcon.DOScale(Vector3.one, 0.15f));
        sendSequence.AppendInterval(0.2f);
        sendSequence.Append(processIcon.DOScale(Vector3.zero, 0.15f));
        sendSequence.Append(successIcon.DOScale(Vector3.one, 0.15f));
    }

    private void CheckFail()
    {
        sendSequence?.Kill();
        sendSequence = DOTween.Sequence();

        checkFamre.alpha = 0;
        checkFamre.gameObject.SetActive(true);
        processIcon.transform.localScale = Vector3.zero;
        successIcon.transform.localScale = Vector3.zero;
        failIcon.transform.localScale = Vector3.zero;
        sendSequence.Append(checkFamre.DOFade(1, 0.15f));
        sendSequence.Append(processIcon.DOScale(Vector3.one, 0.15f));
        sendSequence.AppendInterval(0.2f);
        sendSequence.Append(processIcon.DOScale(Vector3.zero, 0.15f));
        sendSequence.Append(failIcon.DOScale(Vector3.one, 0.15f));
        sendSequence.AppendInterval(0.5f);
        sendSequence.AppendCallback(() =>
        {
            currentDwellTime = 0;
        });
    }


    private void OnPointerEnter(PointerEventData eventData)
    {
        if (isSendData) return;
        iconUIEffect.edgeMode = EdgeMode.Plain;
    }

    private void OnPointerExit(PointerEventData eventData)
    {
        if (isSendData) return;
        iconUIEffect.edgeMode = EdgeMode.None;
    }

    private void OnPointerClick(PointerEventData eventData)
    {
        if (!isSendData)
        {
            OnClick?.Invoke(this);
        }
    }
}

public enum ExhibitionState
{
    Idle,
    
    Waiting,
}
