using UnityEngine;

public class ValueNumberContent : MonoBehaviour
{
    public void SetValue(int count)
    {
        count = Mathf.Clamp(count, 0, transform.childCount);

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.childCount == 0)
                continue;

            child.GetChild(0).gameObject.SetActive(i < count);
        }
    }
}
