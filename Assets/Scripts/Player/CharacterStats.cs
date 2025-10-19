using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public float health = 100f;

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log($"{gameObject.name} health: {health}");
    }
}