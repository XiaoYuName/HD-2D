using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] float stamina;
    
    public float Stamina => stamina;

    public bool CanConsumeStamina(float value)
    {
        return stamina >= value;
    }

    public void SubStamina(float value)
    {
        stamina -= value;
        stamina = Mathf.Max(stamina, 0);
    }
}
