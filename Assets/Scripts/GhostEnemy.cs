using System;
using UnityEngine;

public class GhostEnemy : MonoBehaviour
{
    [Header("Health")]
    public int maxHp = 3;
    [SerializeField] private int hp;

    public int Hp => hp;
    public bool IsDead => hp <= 0;

    /// <summary>
    /// 只要這隻 Ghost「以遊戲邏輯死亡」就會觸發一次。
    /// WaveManager / Spawner 可用它做 alive 計數。
    /// </summary>
    public event Action Died;

    public float moveSpeed = 0.25f;
    public int damageToTower = 10;

    [Header("Assigned at runtime by spawner")]
    public Transform tower;

    // 防止死亡事件重複觸發
    private bool _dead;

    void Awake()
    {
        // Trigger 事件至少要有一個 Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        hp = Mathf.Max(1, maxHp); // 開場滿血
        _dead = false;
    }

    public void TakeDamage(int damage)
    {
        if (_dead) return;

        hp = Mathf.Max(0, hp - Mathf.Max(0, damage));
        if (hp == 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 統一死亡入口：任何原因需要結束這隻怪，都走這裡。
    /// 確保 Died 只觸發一次，並且在 Destroy 前發出事件。
    /// </summary>
    public void Die()
    {
        if (_dead) return;
        _dead = true;

        // 保持狀態一致
        hp = 0;

        Died?.Invoke();
        Destroy(gameObject);
    }

    void Update()
    {
        if (_dead) return;
        if (!tower) return;

        Vector3 toTower = tower.position - transform.position;
        toTower.y = 0f;

        if (toTower.sqrMagnitude < 0.02f * 0.02f) return;

        Vector3 dir = toTower.normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
        transform.forward = dir;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_dead) return;

        var towerHp = other.GetComponentInParent<TowerHealth>();
        if (towerHp)
        {
            towerHp.TakeDamage(damageToTower);
            Die(); // 不要直接 Destroy，避免波次計數漏掉
        }
    }

    void OnDestroy()
    {
        // 保底：如果外部有人直接 Destroy（例如 spawner lifeTime、其他管理器回收）
        // 仍讓 Wave 計數不漏。但在場景卸載或停止播放時不應觸發遊戲事件。
        if (_dead) return;
        if (!Application.isPlaying) return;

        _dead = true;
        hp = 0;
        Died?.Invoke();
    }
}
