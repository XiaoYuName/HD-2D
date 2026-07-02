using Sirenix.OdinInspector;
using UnityEngine;

public class HockWallController : MonoBehaviour
{
    [LabelText("推力")]
    public Vector2 force;
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Doll"))
        {
            Debug.Log("扔出娃娃!");
            if (other.TryGetComponent(out Rigidbody2D rb))
            {
                rb.AddForce(force,ForceMode2D.Impulse);
            }
        }
    }
}
