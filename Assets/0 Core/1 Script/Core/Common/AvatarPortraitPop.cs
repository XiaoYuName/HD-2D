using UnityEngine;
using TMPro;
using UnityEngine.UI;
using XFramework;

public class AvatarPortraitPop : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI contextText;
    
    public void Set(AvatarPortraitData data)
    {
        // nameText.text = data.name;
        // iconImage.sprite = data.icon;
        contextText.text = data.context;
    }
    public void SetContext(string context)
    {
        contextText.text = context;
    }
}

[System.Serializable]
public class AvatarPortraitData
{
    public CharType type;
    public string context;

    public static AvatarPortraitData Create(CharType type, string context)
    {
        return new AvatarPortraitData
        {
            type = type,
            context = context
        };
    }
}
// 角色类型
public enum CharType
{
    
}