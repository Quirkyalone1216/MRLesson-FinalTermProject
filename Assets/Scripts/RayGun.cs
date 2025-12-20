using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RayGun : MonoBehaviour
{
    [Header("Mask / Input / Visuals")]
    [Tooltip("只對這些層做『敵人命中/傷害』。建議只勾 Enemy。")]
    public LayerMask layerMask; // Enemy

    [Tooltip("遮蔽物（例如 Tower）用的層。光束會被它擋住。建議只勾 Tower。")]
    public LayerMask occlusionMask; // Tower

    public OVRInput.RawButton shootingButton = OVRInput.RawButton.RIndexTrigger;
    public LineRenderer linePrefab;

    [Header("Muzzle")]
    public Transform shootingPoint;
    public float maxLineDistance = 5f;
    public float lineShowTimer = 0.3f;

    [Header("Beam Hit")]
    [Tooltip("光束半徑(公尺)。VR 建議 0.02~0.05；越大越容易命中近距離目標。")]
    public float beamRadius = 0.03f;

    [Tooltip("把射線起點往後退(公尺)，避免起點落在 Trigger 內而漏判。")]
    public float rayOriginBackoff = 0.02f;

    [Tooltip("極近距離補救：槍口附近有 Enemy 就算命中(公尺)。")]
    public float closeRangeOverlapRadius = 0.05f;

    [Tooltip("是否穿透：同一發可命中路徑上所有 Ghost（但仍會被 occlusionMask 擋住）。")]
    public bool piercing = true;

    [Header("Damage")]
    public int damagePerShot = 1;

    [Header("Audio")]
    public AudioSource source;
    public AudioClip shootingAudioClip;

    [Header("Impact VFX")]
    public GameObject rayImpactPrefab;
    public float impactTTL = 1.0f;

    private static readonly BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();

        // Enemy mask fallback
        if (layerMask.value == 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1) layerMask = LayerMask.GetMask("Enemy");
        }

        // Occlusion mask fallback（可選）
        if (occlusionMask.value == 0)
        {
            int towerLayer = LayerMask.NameToLayer("Tower");
            if (towerLayer != -1) occlusionMask = LayerMask.GetMask("Tower");
        }
    }

    private void Update()
    {
        if (OVRInput.GetDown(shootingButton))
            Shoot();
    }

    public void Shoot()
    {
        if (shootingAudioClip && source)
            source.PlayOneShot(shootingAudioClip);

        if (!shootingPoint)
        {
            Debug.LogError("[RayGun] shootingPoint 未指定，無法射擊。");
            return;
        }

        Vector3 dir = shootingPoint.forward.normalized;
        Vector3 castOrigin = shootingPoint.position - dir * Mathf.Max(0f, rayOriginBackoff);
        float castDistance = maxLineDistance + Mathf.Max(0f, rayOriginBackoff);

        // 0) 先找「最近遮蔽物」（Tower）。光束以同樣半徑判定遮蔽，避免邊緣穿模。
        bool hasBlock = false;
        float blockDist = float.PositiveInfinity;
        RaycastHit blockHit = default;

        if (occlusionMask.value != 0)
        {
            // 若槍口已經插進 Tower 內，直接視為被擋住（避免怪異穿牆射擊）
            if (Physics.CheckSphere(shootingPoint.position, 0.005f, occlusionMask, QueryTriggerInteraction.Collide))
            {
                hasBlock = true;
                blockDist = 0f;
                blockHit.point = shootingPoint.position;
                blockHit.normal = -dir;
            }
            else if (Physics.SphereCast(
                castOrigin,
                Mathf.Max(0.0001f, beamRadius),
                dir,
                out blockHit,
                castDistance,
                occlusionMask,
                QueryTriggerInteraction.Collide))
            {
                hasBlock = true;
                blockDist = blockHit.distance;
            }
        }

        // 1) 收集敵人命中（近距離補救 + 沿路命中）
        HashSet<GhostEnemy> damaged = new HashSet<GhostEnemy>();
        bool anyEnemyHit = false;

        // 1A) 近距離 Overlap：只接受「在遮蔽之前」的敵人
        Collider[] overlaps = Physics.OverlapSphere(
            shootingPoint.position,
            Mathf.Max(0.0001f, closeRangeOverlapRadius),
            layerMask,
            QueryTriggerInteraction.Collide
        );

        if (overlaps != null && overlaps.Length > 0)
        {
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider c = overlaps[i];
                GhostEnemy enemy = c.GetComponentInParent<GhostEnemy>();
                if (enemy == null) continue;

                // 估計敵人在射線方向上的距離（沿射線投影）
                Vector3 p = c.ClosestPoint(shootingPoint.position);
                float along = Vector3.Dot((p - castOrigin), dir);
                if (along < 0f) continue;

                // 若前方有 Tower 且敵人在 Tower 後面 -> 不算命中
                if (hasBlock && along > blockDist + 1e-3f) continue;

                if (damaged.Add(enemy))
                {
                    ApplyDamageAndMaybeKill(enemy, damagePerShot);
                    anyEnemyHit = true;
                    if (!piercing) break;
                }
            }
        }

        // 1B) 沿路 SphereCastAll：只處理「在 Tower 之前」的命中
        RaycastHit[] enemyHits = Physics.SphereCastAll(
            castOrigin,
            Mathf.Max(0.0001f, beamRadius),
            dir,
            castDistance,
            layerMask,
            QueryTriggerInteraction.Collide
        );

        if (enemyHits != null && enemyHits.Length > 0)
        {
            Array.Sort(enemyHits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < enemyHits.Length; i++)
            {
                // Tower 擋住後方一律忽略
                if (hasBlock && enemyHits[i].distance > blockDist + 1e-3f)
                    break;

                GhostEnemy enemy = enemyHits[i].collider.GetComponentInParent<GhostEnemy>();
                if (enemy == null) continue;

                if (damaged.Add(enemy))
                {
                    ApplyDamageAndMaybeKill(enemy, damagePerShot);
                    anyEnemyHit = true;
                    if (!piercing) break;
                }
            }
        }

        // 2) 決定光束終點：最近的「敵人命中點」 vs 「Tower 擋住點」
        Vector3 endPoint = shootingPoint.position + dir * maxLineDistance;
        bool hasEnemyImpactPoint = (enemyHits != null && enemyHits.Length > 0);

        // 找第一個有效敵人命中點（且在 Tower 前）
        RaycastHit firstEnemyHit = default;
        bool foundFirstEnemyHit = false;

        if (hasEnemyImpactPoint)
        {
            Array.Sort(enemyHits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < enemyHits.Length; i++)
            {
                if (hasBlock && enemyHits[i].distance > blockDist + 1e-3f) break;
                if (enemyHits[i].collider.GetComponentInParent<GhostEnemy>() != null)
                {
                    firstEnemyHit = enemyHits[i];
                    foundFirstEnemyHit = true;
                    break;
                }
            }
        }

        bool useBlockPoint = hasBlock && (!foundFirstEnemyHit || blockDist < firstEnemyHit.distance);

        if (useBlockPoint)
            endPoint = blockHit.point;
        else if (foundFirstEnemyHit)
            endPoint = firstEnemyHit.point;

        // 3) Impact VFX：打到塔就出現在塔上；打到敵人就出現在敵人上
        if (rayImpactPrefab)
        {
            if (useBlockPoint)
            {
                Quaternion rot = Quaternion.LookRotation(-blockHit.normal);
                GameObject fx = Instantiate(rayImpactPrefab, blockHit.point, rot);
                if (impactTTL > 0f) Destroy(fx, impactTTL);
            }
            else if (foundFirstEnemyHit)
            {
                Quaternion rot = Quaternion.LookRotation(-firstEnemyHit.normal);
                GameObject fx = Instantiate(rayImpactPrefab, firstEnemyHit.point, rot);
                if (impactTTL > 0f) Destroy(fx, impactTTL);
            }
        }

        // 4) LineRenderer
        if (linePrefab)
        {
            LineRenderer line = Instantiate(linePrefab);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, shootingPoint.position);
            line.SetPosition(1, endPoint);
            Destroy(line.gameObject, lineShowTimer);
        }

        if (anyEnemyHit)
            Debug.Log($"[RayGun] Enemy hit. targets={damaged.Count}, piercing={piercing}, blockedByTower={hasBlock}");
        else if (hasBlock)
            Debug.Log("[RayGun] Blocked by Tower (occlusion).");
    }

    private void ApplyDamageAndMaybeKill(GhostEnemy enemy, int amount)
    {
        MethodInfo takeDamage = enemy.GetType().GetMethod("TakeDamage", AnyInstance, null, new[] { typeof(int) }, null);
        if (takeDamage != null)
        {
            takeDamage.Invoke(enemy, new object[] { amount });
            return;
        }

        if (TryReadWriteInt(enemy, "Hp", -amount, out int newHp) ||
            TryReadWriteInt(enemy, "hp", -amount, out newHp) ||
            TryReadWriteInt(enemy, "HP", -amount, out newHp))
        {
            if (newHp <= 0) Destroy(enemy.gameObject);
            return;
        }

        Debug.LogWarning($"[RayGun] 命中 {enemy.name} 但找不到 TakeDamage(int) 或 Hp/hp 欄位。");
    }

    private bool TryReadWriteInt(object target, string memberName, int delta, out int newValue)
    {
        newValue = 0;
        Type t = target.GetType();

        PropertyInfo p = t.GetProperty(memberName, AnyInstance);
        if (p != null && p.PropertyType == typeof(int) && p.CanRead && p.CanWrite)
        {
            int old = (int)p.GetValue(target);
            newValue = old + delta;
            p.SetValue(target, newValue);
            return true;
        }

        FieldInfo f = t.GetField(memberName, AnyInstance);
        if (f != null && f.FieldType == typeof(int))
        {
            int old = (int)f.GetValue(target);
            newValue = old + delta;
            f.SetValue(target, newValue);
            return true;
        }

        return false;
    }
}
