using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References (建議手動指定；未指定會自動尋找)")]
    public TowerHealth towerHealth;

    [Header("Game Over Behavior")]
    public bool disableSpawners = true;
    public bool disableRayGuns = true;
    public bool clearGhosts = true;

    private GhostSpawner[] spawners;
    private RayGun[] rayGuns;
    private bool handled;

    private void Awake()
    {
        // 建議：Inspector 指定最穩；但為了驗收容錯，提供自動尋找
#if UNITY_2023_1_OR_NEWER
        if (!towerHealth)
            towerHealth = FindFirstObjectByType<TowerHealth>(FindObjectsInactive.Include);

        spawners = FindObjectsByType<GhostSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        rayGuns = FindObjectsByType<RayGun>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        if (!towerHealth)
            towerHealth = FindObjectOfType<TowerHealth>(true);

        spawners = FindObjectsOfType<GhostSpawner>(true);
        rayGuns = FindObjectsOfType<RayGun>(true);
#endif
    }

    private void OnEnable()
    {
        if (towerHealth != null)
            towerHealth.Died += OnTowerDied;
    }

    private void OnDisable()
    {
        if (towerHealth != null)
            towerHealth.Died -= OnTowerDied;
    }

    private void OnTowerDied()
    {
        if (handled) return;
        handled = true;

        if (disableSpawners && spawners != null)
        {
            foreach (var s in spawners)
                if (s) s.enabled = false;
        }

        if (disableRayGuns && rayGuns != null)
        {
            foreach (var g in rayGuns)
                if (g) g.enabled = false;
        }

        if (clearGhosts)
        {
#if UNITY_2023_1_OR_NEWER
            var enemies = FindObjectsByType<GhostEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var enemies = FindObjectsOfType<GhostEnemy>(true);
#endif
            foreach (var e in enemies)
                if (e) Destroy(e.gameObject);
        }

        Debug.Log("[GameManager] Game Over handled: Spawners/Guns disabled, Ghosts cleared.");
    }
}
