using UnityEngine;

public class ValueNumberContent : MonoBehaviour
{
    public void SetValue(int count)
    {
        for (int i = 0; i < transform.childCount - 1; i++)
        {
            transform.GetChild(i).GetChild(0).gameObject.SetActive(i < count);
        }
    }
}
