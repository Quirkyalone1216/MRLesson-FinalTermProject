using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("References (建議手動指定；未指定會自動尋找)")]
    public TowerHealth towerHealth;

    [Header("Game Over UI")]
    [Tooltip("Game Over 時要顯示的 UI（例如 Canvas 或 Panel）。建議在 Inspector 先設為 Inactive。")]
    public GameObject gameOverUI;

    [Header("Game Over Behavior")]
    public bool disableSpawners = true;
    public bool disableRayGuns = true;
    public bool clearGhosts = true;

    [Header("Restart (保險機制)")]
    [Tooltip("Game Over 後允許按 R 重新開始（避免 VR UI 射線沒設好按不到）。")]
    public bool allowKeyboardRestart = true;

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

    private void Start()
    {
        // 確保遊戲開始時 UI 不會誤開
        if (gameOverUI) gameOverUI.SetActive(false);
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

    private void Update()
    {
        if (!handled) return;

        // 這是保險：就算 VR UI 互動沒配好，也能重開避免展示翻車
        if (allowKeyboardRestart && Input.GetKeyDown(KeyCode.R))
            RestartGame();
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

        if (gameOverUI) gameOverUI.SetActive(true);

        Debug.Log("[GameManager] Game Over handled: Spawners/Guns disabled, Ghosts cleared. UI shown.");
    }

    // 給 UI Button 的 OnClick() 直接呼叫
    public void RestartGame()
    {
        // 若你未來有做 Time.timeScale = 0 的暫停，這行可以避免重開後時間卡住
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
