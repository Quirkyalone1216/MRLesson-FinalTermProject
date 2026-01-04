using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    [Header("References (建議手動指定；未指定會自動尋找)")]
    public TowerHealth towerHealth;

    [Header("Game Over UI")]
    [Tooltip("Game Over 時要顯示的 UI（例如 Canvas 或 Panel）。建議在 Inspector 先設為 Inactive。")]
    public GameObject gameOverUI;

    [Tooltip("Game Over 畫面中「Restart」按鈕（或任一可選取 UI 物件）。用於自動設置 UI 焦點，讓 A/Submit 一按就生效。")]
    public GameObject restartButtonGO;

    [Header("Game Over Behavior")]
    public bool disableSpawners = true;
    public bool disableRayGuns = true;
    public bool clearGhosts = true;

    [Header("Restart (保險機制)")]
    [Tooltip("Game Over 後允許按 R 重新開始（桌機/Editor 測試用）。")]
    public bool allowKeyboardRestart = true;

    [Tooltip("Game Over 後允許按控制器按鍵直接重開（不依賴 UI 射線/焦點，最穩）。")]
    public bool allowControllerRestart = true;

    [Tooltip("控制器重開按鍵（預設 A）。")]
    public OVRInput.RawButton controllerRestartButton = OVRInput.RawButton.A;

    [Tooltip("為避免 OpenXR/控制器狀態切換時 GetDown 漏掉，可允許『按住』按鍵達到秒數後也能重開。")]
    public bool allowHoldToRestart = true;

    [Tooltip("按住重開所需時間（秒，使用 UnscaledTime）。")]
    public float holdToRestartSeconds = 0.25f;

    [Tooltip("重開去彈跳（秒），避免一個輸入觸發多次 LoadScene。")]
    public float restartDebounceSeconds = 0.25f;

    private GhostSpawner[] spawners;
    private RayGun[] rayGuns;

    private bool handled;
    private bool restartTriggered;

    private float lastRestartTime = -999f;
    private float holdRestartTimer = 0f;

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
        if (!handled || restartTriggered) return;

        // 保險：就算 VR UI 互動沒配好，也能重開避免展示翻車
        if (allowKeyboardRestart && Input.GetKeyDown(KeyCode.R))
        {
            TryRestart();
            return;
        }

        // 最穩：GameOver 後直接用控制器按鍵重開（不依賴 UI 焦點/射線）
        if (allowControllerRestart)
        {
            // debounce：避免連續觸發
            if (Time.unscaledTime - lastRestartTime < restartDebounceSeconds)
                return;

            // 1) 按下瞬間
            if (OVRInput.GetDown(controllerRestartButton))
            {
                lastRestartTime = Time.unscaledTime;
                TryRestart();
                return;
            }

            // 2) 按住達成（可抵抗 InteractionProfileChanged 時短暫漏抓 GetDown）
            if (allowHoldToRestart && OVRInput.Get(controllerRestartButton))
            {
                holdRestartTimer += Time.unscaledDeltaTime;
                if (holdRestartTimer >= holdToRestartSeconds)
                {
                    lastRestartTime = Time.unscaledTime;
                    TryRestart();
                    return;
                }
            }
            else
            {
                holdRestartTimer = 0f;
            }
        }
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

        // UI 焦點保險：讓 A/Submit 也能穩定點到 Restart（即使你仍想靠 UI 操作）
        if (restartButtonGO != null)
            StartCoroutine(FocusRestartButtonNextFrame());

        Debug.Log("[GameManager] Game Over handled: Spawners/Guns disabled, Ghosts cleared. UI shown.");
    }

    private IEnumerator FocusRestartButtonNextFrame()
    {
        // 等一個 frame，避免剛 SetActive(true) 時 EventSystem/Selectable 尚未 ready
        yield return null;

        if (EventSystem.current == null) yield break;

        // 清掉舊選取避免某些 module 不更新
        EventSystem.current.SetSelectedGameObject(null);

        // 設定焦點到 Restart
        EventSystem.current.SetSelectedGameObject(restartButtonGO);
    }

    private void TryRestart()
    {
        if (restartTriggered) return;
        restartTriggered = true;

        RestartGame();
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
