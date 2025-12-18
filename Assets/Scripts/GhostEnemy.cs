using UnityEngine;

public class GhostEnemy : MonoBehaviour
{
    public int hp = 1;
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
