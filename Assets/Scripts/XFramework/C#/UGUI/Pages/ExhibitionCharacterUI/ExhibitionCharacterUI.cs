using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public partial class ExhibitionCharacterUI : UIBase
{
    public ExhibitionState State { get; private set; } = ExhibitionState.Idle;
    private Sequence _sequence;

    [LabelText("展示槽位")]
    public List<FlySlot> FlySlots;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        infoUI.transform.localScale = Vector3.zero;
        for (int i = 0; i < FlySlots.Count; i++)
        {
            FlySlots[i].gameObject.SetActive(false);
            FlySlots[i].transform.localScale = Vector3.zero;
            FlySlots[i].Init();
        }
    }

    public void SetData(ExhibitionGameData exhibitionGameData)
    {
        if (State != ExhibitionState.Idle) return;
        State = ExhibitionState.Waiting;
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

    public IEnumerator Dwell()
    {
        float dwellTime = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        dwellSlider.minValue = 0;
        dwellSlider.maxValue = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        dwellSlider.value = ExhibitionManager.Instance.ExhibitionInfoData.DwellTime;
        while (dwellTime > 0)
        {
            dwellTime -= Time.deltaTime;
            dwellSlider.value = dwellTime;
            yield return null;
        }
        dwellTime = 0;
        dwellSlider.value = dwellTime;
        
        _sequence = DOTween.Sequence();
        _sequence.Append(infoUI.transform.DOScale(Vector3.zero, 0.35f));
        _sequence.AppendInterval(0.5f);
        _sequence.Append(exhibitionCharacterUI.DOFade(0, 0.15f));
        yield return _sequence.WaitForCompletion();
        State = ExhibitionState.Idle;
    }
}

public enum ExhibitionState
{
    Idle,
    
    Waiting,
}
