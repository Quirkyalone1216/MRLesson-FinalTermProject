using System;
using UnityEngine;

public class GhostEnemy : MonoBehaviour
{
    [Header("Health")]
    public int maxHp = 3;
    [SerializeField] private int hp;

    public int Hp => hp;
    public bool IsDead => hp <= 0;
    public event Action Died;

    public float moveSpeed = 0.25f;
    public int damageToTower = 10;

    [Header("Assigned at runtime by spawner")]
    public Transform tower;

    void Awake()
    {
        // Trigger 事件至少要有一個 Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        hp = Mathf.Max(1, maxHp); // 開場滿血
    }

    public void TakeDamage(int damage)
    {
        if (hp <= 0) return;
        hp = Mathf.Max(0, hp - Mathf.Max(0, damage));

        if (hp == 0)
        {
            Died?.Invoke();
            Destroy(gameObject);
        }
    }

    void Update()
    {
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
        var towerHp = other.GetComponentInParent<TowerHealth>();
        if (towerHp)
        {
            towerHp.TakeDamage(damageToTower);
            Destroy(gameObject);
        }
    }
}
