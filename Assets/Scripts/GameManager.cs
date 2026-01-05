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

    [Tooltip("Game Over 畫面中「Restart」按鈕（或任一可選取 UI 物件）。用於自動設置 UI 焦點。")]
    public GameObject restartButtonGO;

    [Header("Victory UI")]
    [Tooltip("Victory 時要顯示的 UI（例如 Canvas 或 Panel）。建議在 Inspector 先設為 Inactive。")]
    public GameObject victoryUI;

    [Tooltip("Victory 畫面中「Restart」按鈕（或任一可選取 UI 物件）。若不填，會 fallback 用 restartButtonGO。")]
    public GameObject victoryRestartButtonGO;

    [Header("End State Behavior")]
    public bool disableSpawners = true;
    public bool disableRayGuns = true;

    [Tooltip("Victory/GameOver 時是否停用 WaveManager（保證不再進入下一波）。")]
    public bool disableWaveManagers = true;

    public bool clearGhosts = true;

    [Header("Restart Controls")]
    [Tooltip("Game Over/Victory 後允許按 R 重新開始（桌機/Editor 測試用）。")]
    public bool allowKeyboardRestart = true;

    [Tooltip("Game Over/Victory 後允許用控制器按鍵重開。若你懷疑誤觸導致場景重載與血量回滿，先關掉這個最有效。")]
    public bool allowControllerRestart = false;

    [Tooltip("控制器重開按鍵（預設 A）。")]
    public OVRInput.RawButton controllerRestartButton = OVRInput.RawButton.A;

    [Tooltip("只允許『長按』重開（避免 GetDown 或 UI 焦點造成誤觸）。")]
    public bool requireHoldToRestart = true;

    [Tooltip("按住重開所需時間（秒，使用 UnscaledTime）。建議 >= 1.0 秒降低誤觸。")]
    public float holdToRestartSeconds = 1.0f;

    [Tooltip("重開去彈跳（秒），避免一個輸入觸發多次 LoadScene。")]
    public float restartDebounceSeconds = 0.5f;

    [Header("Safety / Debug")]
    [Tooltip("結束畫面出現後，延遲幾秒才允許輸入重開，避免控制器狀態抖動或誤觸。")]
    public float restartInputGraceSeconds = 0.75f;

    [Tooltip("每次重開前印出 StackTrace，直接抓『誰觸發 Scene 重載』。")]
    public bool logRestartStackTrace = true;

    [Tooltip("是否自動把 UI 焦點設到 Restart 按鈕。若你覺得 A/Submit 會誤觸，建議關掉。")]
    public bool autoFocusRestartButton = false;

    private GhostSpawner[] spawners;
    private RayGun[] rayGuns;
    private WaveManager[] waveManagers;

    private bool handled;
    private bool restartTriggered;

    private float lastRestartTime = -999f;
    private float holdRestartTimer = 0f;
    private float endShownTime = -999f;

    private enum EndState { None, GameOver, Victory }
    private EndState endState = EndState.None;

    private void Awake()
    {
        // 建議：Inspector 指定最穩；但為了驗收容錯，提供自動尋找
#if UNITY_2023_1_OR_NEWER
        if (!towerHealth)
            towerHealth = FindFirstObjectByType<TowerHealth>(FindObjectsInactive.Include);

        spawners = FindObjectsByType<GhostSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        rayGuns = FindObjectsByType<RayGun>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        waveManagers = FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        if (!towerHealth)
            towerHealth = FindObjectOfType<TowerHealth>(true);

        spawners = FindObjectsOfType<GhostSpawner>(true);
        rayGuns = FindObjectsOfType<RayGun>(true);
        waveManagers = FindObjectsOfType<WaveManager>(true);
#endif
    }

    private void Start()
    {
        // 確保遊戲開始時 UI 不會誤開
        if (gameOverUI) gameOverUI.SetActive(false);
        if (victoryUI) victoryUI.SetActive(false);
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

        // 結束 UI 出現後，先給一段 grace time，避免誤觸
        if (Time.unscaledTime - endShownTime < restartInputGraceSeconds)
            return;

        // 桌機測試：R 重開
        if (allowKeyboardRestart && Input.GetKeyDown(KeyCode.R))
        {
            TryRestart("Keyboard:R");
            return;
        }

        // 控制器重開
        if (allowControllerRestart)
        {
            // debounce：避免連續觸發
            if (Time.unscaledTime - lastRestartTime < restartDebounceSeconds)
                return;

            if (requireHoldToRestart)
            {
                // 只允許長按，降低誤觸
                if (OVRInput.Get(controllerRestartButton))
                {
                    holdRestartTimer += Time.unscaledDeltaTime;
                    if (holdRestartTimer >= holdToRestartSeconds)
                    {
                        lastRestartTime = Time.unscaledTime;
                        TryRestart($"Controller:Hold({controllerRestartButton})");
                        return;
                    }
                }
                else
                {
                    holdRestartTimer = 0f;
                }
            }
            else
            {
                // 允許按下瞬間
                if (OVRInput.GetDown(controllerRestartButton))
                {
                    lastRestartTime = Time.unscaledTime;
                    TryRestart($"Controller:Down({controllerRestartButton})");
                    return;
                }
            }
        }
    }

    private void OnTowerDied()
    {
        HandleEnd(EndState.GameOver);
    }

    // 由 WaveManager 呼叫（Victory）
    public void HandleVictory()
    {
        HandleEnd(EndState.Victory);
    }

    private void HandleEnd(EndState state)
    {
        if (handled) return;

        handled = true;
        endState = state;

        if (disableWaveManagers && waveManagers != null)
        {
            foreach (var w in waveManagers)
                if (w) w.enabled = false;
        }

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

        // UI 切換
        if (state == EndState.GameOver)
        {
            if (victoryUI) victoryUI.SetActive(false);
            if (gameOverUI) gameOverUI.SetActive(true);
        }
        else // Victory
        {
            if (gameOverUI) gameOverUI.SetActive(false);
            if (victoryUI) victoryUI.SetActive(true);
        }

        endShownTime = Time.unscaledTime;

        // 若你覺得 A/Submit 會誤觸，請把 autoFocusRestartButton 關掉
        if (autoFocusRestartButton)
        {
            var focusTarget = (state == EndState.Victory && victoryRestartButtonGO != null)
                ? victoryRestartButtonGO
                : restartButtonGO;

            if (focusTarget != null)
                StartCoroutine(FocusSelectedNextFrame(focusTarget));
        }

        Debug.Log($"[GameManager] End handled: state={state}, Spawners/Guns/Waves disabled, Ghosts cleared. UI shown.");
    }

    private IEnumerator FocusSelectedNextFrame(GameObject target)
    {
        yield return null;

        if (EventSystem.current == null) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private void TryRestart(string reason)
    {
        if (restartTriggered) return;
        restartTriggered = true;

        Debug.Log($"[GameManager] Restart triggered. reason={reason}");

        RestartGame();
    }

    // 給 UI Button 的 OnClick() 直接呼叫（GameOver/Victory 共用）
    public void RestartGame()
    {
        if (logRestartStackTrace)
        {
            Debug.Log("[GameManager] RestartGame() called. StackTrace:\n" + System.Environment.StackTrace);
        }

        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
