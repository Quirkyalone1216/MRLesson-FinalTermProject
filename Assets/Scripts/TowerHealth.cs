using System;
using UnityEngine;

public class TowerHealth : MonoBehaviour
{
    public int maxHp = 100;
    [SerializeField] private int hp;

    public int Hp => hp;
    public bool IsDead => hp <= 0;

    /// <summary>塔死亡事件：hp 由 >0 變成 0 時觸發一次</summary>
    public event Action Died;

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
        {
            Debug.Log("[Tower] Game Over");
            Died?.Invoke();
        }
    }
}
