using UnityEngine;
using TMPro;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 画作结算弹窗：评分达标展示成品图与品级，未达标展示废稿提示；奖励就是这张画稿本身，已在结算时进物品栏。
/// </summary>
public class MachiRoomPaintResultState : MonoBehaviour
{
    [SerializeField] Button backButton;
    [SerializeField] MachiRoomGamePanel panel;
    [SerializeField] GameObject successObject, failObject;
    [SerializeField] LocalizeStringEvent scoreText;
    [SerializeField] TextMeshProUGUI successGradeText;
    [SerializeField] LocalizeStringEvent successTipText;
    [SerializeField] LocalizeStringEvent failTipText;
    [SerializeField] LocalizeStringEvent nameText;
    [SerializeField] Image paintImage;

    const string LocKeyPrefix = "MachiRoom/";
    const string Score = LocKeyPrefix + nameof(Score);
    const string DraftStored = LocKeyPrefix + nameof(DraftStored);
    const string PoorArtwork = LocKeyPrefix + nameof(PoorArtwork);

    void Awake()
    {
        backButton.onClick.AddListener(OnBack);
    }

    public void ShowSuccess()
    {
        RefreshCommon(true);
        successGradeText.text = panel.GetQualityText();
        successTipText.SetText(LocTableSet.MachiRoom, DraftStored);
        paintImage.SetIcon(MachiRoomGameManager.Instance.GetFinalImagePath());
    }

    public void ShowFail()
    {
        RefreshCommon(false);
        failTipText.SetText(LocTableSet.MachiRoom, PoorArtwork);
        paintImage.SetIcon(MachiRoomGameManager.Instance.GetDraftImagePath());
    }

    void RefreshCommon(bool success)
    {
        successObject.SetActive(success);
        failObject.SetActive(!success);

        MachiRoomCreationInfo creationInfo = MachiRoomGameManager.Instance.CreationInfo;
        scoreText.SetTextWithVar(LocTableSet.MachiRoom, Score, LocVarSet.Score, creationInfo.Score);
        ItemData itemData = InventoryManager.Instance.GetItemData(creationInfo.ManuscriptItemId);
        nameText.SetText(itemData.NameKey.Table, itemData.NameKey.Value);
    }

    void OnBack()
    {
        paintImage.ClearIcon();
        panel.OnResultBack();
    }
}
