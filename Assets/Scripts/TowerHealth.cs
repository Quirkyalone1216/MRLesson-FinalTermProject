using UnityEngine;

public class TowerHealth : MonoBehaviour
{
    public int maxHp = 100;
    [SerializeField] int hp;

    void Awake()
    {
        hp = maxHp;
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            hp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        if (hp <= 0) return;

        hp = Mathf.Max(0, hp - damage);
        Debug.Log($"[Tower] HP: {hp}/{maxHp}");

        if (hp == 0)
            Debug.Log("[Tower] Game Over");
    }
}
