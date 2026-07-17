using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

public class AvatarPortraitPop : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] LocalizeStringEvent contextText;
    
    public void Set(AvatarPortraitData data)
    {
        // nameText.text = data.name;
        // iconImage.sprite = data.IconPath;
        
    }
    public void SetContext(string contentTable, string contentKey)
    {
        contextText.SetText(contentTable, contentKey);
    }
}

[System.Serializable]
public class AvatarPortraitData
{
    public CharType type;
    public string contentTable;
    public string contentKey;

    public static AvatarPortraitData Create(CharType type, string contentTable, string contentKey)
    {
        return new AvatarPortraitData
        {
            type = type,
            contentTable = contentTable,
            contentKey = contentKey
        };
    }
}
// 角色类型
public enum CharType
{
    
}