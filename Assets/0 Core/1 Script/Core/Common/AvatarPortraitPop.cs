using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

public class AvatarPortraitPop : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] LocalizeStringEvent contextText;
    
    public void Set(AvatarPortraitInfo data)
    {
        
    }
    public void SetContext(string contentTable, string contentKey)
    {
        contextText.SetText(contentTable, contentKey);
    }
}

[System.Serializable]
public class AvatarPortraitInfo
{
    public long charaId;
    public string contentTable;
    public string contentKey;

    public static AvatarPortraitInfo Create(long charaId, string contentTable, string contentKey)
    {
        return new AvatarPortraitInfo
        {
            charaId = charaId,
            contentTable = contentTable,
            contentKey = contentKey
        };
    }
}
