using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

public class WaveManager : MonoBehaviour
{
    [Header("Waves (Optional)")]
    [Tooltip("Can be empty: if waves is empty and autoGenerateWavesIfEmpty=true, Start() will auto-build default waves.")]
    public List<WaveDefinition> waves = new();

    [Header("Auto Generate Waves")]
    public bool autoGenerateWavesIfEmpty = true;

    [Min(1)] public int autoWaveCount = 3;

    [Tooltip("If WaveDefinition.enemyPrefabs is empty, use this; if this is also empty, fallback to spawner.prefabToSpawn.")]
    public GameObject[] defaultEnemyPrefabs;

    [Header("Auto Generate Formula")]
    [Min(1)] public int baseEnemies = 5;
    [Min(0)] public int enemiesIncreasePerWave = 3;

    [Min(0.05f)] public float baseSpawnInterval = 1.0f;
    [Min(0f)] public float spawnIntervalDecreasePerWave = 0.08f;
    [Min(0.05f)] public float minSpawnInterval = 0.25f;

    [Min(1)] public int baseMaxConcurrent = 2;
    [Min(0)] public int maxConcurrentIncreasePerWave = 1;

    [Min(0f)] public float defaultPreWaveDelay = 2f;
    [Min(0f)] public float defaultPostWaveDelay = 1f;

    [Header("Spawn Fail Safety")]
    [Tooltip("If TrySpawn fails too many times in a row, stop to avoid an infinite loop.")]
    [Min(1)] public int maxConsecutiveSpawnFails = 50;

    [Header("References")]
    public GhostSpawner spawner;
    public TowerHealth towerHealth;

    [Tooltip("Recommend: notify GameManager on Victory. If not set, will fallback to victoryUI.")]
    public GameManager gameManager;

    [Header("UI (Fallback)")]
    [Tooltip("If gameManager is not set, use this to show Victory UI (not recommended long-term).")]
    public GameObject victoryUI;

    [Header("UI - Wave Status (TextMeshPro)")]
    [Tooltip("Drag a TextMeshProUGUI / TMP_Text from your Canvas here. Optional.")]
    public TMP_Text waveStatusTMP;

    [Tooltip("Show wave status text")]
    public bool showWaveStatus = true;

    [Tooltip("Show detailed progress (Alive / Spawned). Turn off if you want simple text.")]
    public bool showDetailedProgress = true;

    [TextArea] public string textPreparing = "Preparing...";
    [TextArea] public string textWaveStartFormat = "Wave {0}/{1} (Remaining {2})";
    [TextArea] public string textWaveProgressFormat = "Wave {0}/{1} (Remaining {2})\nAlive: {3}   Spawned: {4}/{5}";
    [TextArea] public string textWaveClearedFormat = "Wave {0}/{1} cleared (Remaining {2})";
    [TextArea] public string textVictory = "Victory! Press A to restart";
    [TextArea] public string textStoppedFormat = "Stopped: {0}";

    private int _waveIndex = -1;
    private int _alive = 0;
    private bool _stopped = false;

    // For UI display (current wave progress)
    private int _currentWaveSpawned = 0;
    private int _currentWaveTarget = 0;

    private void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!towerHealth)
            towerHealth = FindFirstObjectByType<TowerHealth>(FindObjectsInactive.Include);

        if (!gameManager)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
#else
        if (!towerHealth)
            towerHealth = FindObjectOfType<TowerHealth>(true);

        if (!gameManager)
            gameManager = FindObjectOfType<GameManager>(true);
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

    IEnumerator Start()
    {
        SetWaveText(textPreparing);

        // 1) Auto-build waves if needed (no Wave_01/02/03 assets required)
        AutoBuildWavesIfNeeded();

        // 2) Wait for MRUK initialization
        while (MRUK.Instance == null || !MRUK.Instance.IsInitialized) yield return null;

        // 3) Wave mode: must disable spawner autoSpawn
        if (spawner) spawner.autoSpawn = false;

        yield return RunAllWaves();
    }

    private void OnTowerDied()
    {
        StopWaves("Tower died (GameOver)");
    }

    private void StopWaves(string reason)
    {
        if (_stopped) return;
        _stopped = true;

        // Stop all coroutines to prevent continuing into next wave after GameOver
        StopAllCoroutines();

        // Ensure no more spawning
        if (spawner)
        {
            spawner.autoSpawn = false;
            spawner.enabled = false;
            spawner.DespawnAll();
        }

        SetWaveText(string.Format(textStoppedFormat, reason));
        Debug.Log($"[WaveManager] Stopped: {reason}");
        enabled = false;
    }

    private void AutoBuildWavesIfNeeded()
    {
        if (!autoGenerateWavesIfEmpty) return;
        if (waves == null) waves = new List<WaveDefinition>();
        if (waves.Count > 0) return;

        GameObject[] fallbackList =
            (defaultEnemyPrefabs != null && defaultEnemyPrefabs.Length > 0) ? defaultEnemyPrefabs : null;

        if ((fallbackList == null || fallbackList.Length == 0) &&
            spawner != null && spawner.prefabToSpawn != null)
        {
            fallbackList = new GameObject[] { spawner.prefabToSpawn };
        }

        if (fallbackList == null || fallbackList.Length == 0)
        {
            Debug.LogError("[WaveManager] Cannot auto-build waves: please set defaultEnemyPrefabs or GhostSpawner.prefabToSpawn.");
            return;
        }

        waves = new List<WaveDefinition>(Mathf.Max(1, autoWaveCount));

        for (int i = 0; i < autoWaveCount; i++)
        {
            var w = ScriptableObject.CreateInstance<WaveDefinition>();
            w.hideFlags = HideFlags.HideAndDontSave;

            w.enemiesToSpawn = baseEnemies + enemiesIncreasePerWave * i;

            float interval = baseSpawnInterval - spawnIntervalDecreasePerWave * i;
            w.spawnInterval = Mathf.Max(minSpawnInterval, interval);

            w.maxConcurrent = Mathf.Max(1, baseMaxConcurrent + maxConcurrentIncreasePerWave * i);

            w.preWaveDelay = defaultPreWaveDelay;
            w.postWaveDelay = defaultPostWaveDelay;

            w.enemyPrefabs = fallbackList;

            waves.Add(w);
        }

        Debug.Log($"[WaveManager] Auto-generated {waves.Count} waves (no Wave_01/02/03 assets needed).");
    }

    private IEnumerator RunAllWaves()
    {
        if (spawner == null)
        {
            Debug.LogError("[WaveManager] spawner is not set. Drag GhostSpawner into WaveManager.spawner.");
            yield break;
        }

        if (waves == null || waves.Count == 0)
        {
            Debug.LogError("[WaveManager] waves is empty. Enable autoGenerateWavesIfEmpty or assign waves manually.");
            yield break;
        }

        for (int i = 0; i < waves.Count; i++)
        {
            if (_stopped) yield break;

            _waveIndex = i;
            var w = waves[i];
            if (w == null) continue;

            _currentWaveSpawned = 0;
            _currentWaveTarget = Mathf.Max(0, w.enemiesToSpawn);

            UpdateWaveText(isCleared: false);

            Debug.Log($"[WaveManager] Wave {_waveIndex + 1}/{waves.Count} start. Spawn={w.enemiesToSpawn}, Interval={w.spawnInterval}, MaxConc={w.maxConcurrent}");

            // 1) Pre-wave delay
            if (w.preWaveDelay > 0f)
                yield return new WaitForSeconds(w.preWaveDelay);

            // 2) Spawn this wave
            int spawned = 0;
            int consecutiveFails = 0;

            while (spawned < w.enemiesToSpawn)
            {
                if (_stopped) yield break;

                if (_alive < w.maxConcurrent)
                {
                    var prefab = w.PickPrefab();
                    if (prefab == null) prefab = spawner.prefabToSpawn;

                    if (prefab != null && spawner.TrySpawn(prefab, out var enemy))
                    {
                        spawned++;
                        consecutiveFails = 0;

                        _currentWaveSpawned = spawned;

                        if (enemy != null)
                        {
                            _alive++;
                            enemy.Died += OnEnemyDied;
                        }

                        UpdateWaveText(isCleared: false);
                    }
                    else
                    {
                        consecutiveFails++;
                        if (consecutiveFails >= maxConsecutiveSpawnFails)
                        {
                            Debug.LogError($"[WaveManager] Wave {_waveIndex + 1} spawn failed {consecutiveFails} times consecutively. Aborting. Check MRUK scan, Spawn Labels, or prefab setup.");
                            yield break;
                        }
                    }
                }

                if (w.spawnInterval > 0f)
                    yield return new WaitForSeconds(w.spawnInterval);
                else
                    yield return null;
            }

            // 3) Wait until this wave is cleared
            while (_alive > 0)
            {
                if (_stopped) yield break;
                yield return null;
            }

            UpdateWaveText(isCleared: true);
            Debug.Log($"[WaveManager] Wave {_waveIndex + 1}/{waves.Count} cleared.");

            // 4) Post-wave delay
            if (w.postWaveDelay > 0f)
                yield return new WaitForSeconds(w.postWaveDelay);
        }

        HandleVictory();
    }

    private void OnEnemyDied()
    {
        _alive = Mathf.Max(0, _alive - 1);
        UpdateWaveText(isCleared: false);
    }

    private void HandleVictory()
    {
        if (_stopped) return;
        _stopped = true;

        if (spawner)
        {
            spawner.autoSpawn = false;
            spawner.enabled = false;
            spawner.DespawnAll();
        }

        if (gameManager != null)
        {
            gameManager.HandleVictory();
        }
        else
        {
            if (victoryUI) victoryUI.SetActive(true);
        }

        SetWaveText(textVictory);
        Debug.Log("[WaveManager] Victory!");
        enabled = false;
    }

    private void UpdateWaveText(bool isCleared)
    {
        if (!showWaveStatus || waveStatusTMP == null) return;
        if (waves == null || waves.Count <= 0) return;

        int current = Mathf.Clamp(_waveIndex + 1, 1, waves.Count);
        int total = waves.Count;

        // Remaining waves (excluding current wave)
        int remainingAfterThis = Mathf.Max(0, total - current);

        if (isCleared)
        {
            waveStatusTMP.text = string.Format(textWaveClearedFormat, current, total, remainingAfterThis);
            return;
        }

        if (!showDetailedProgress)
        {
            waveStatusTMP.text = string.Format(textWaveStartFormat, current, total, remainingAfterThis);
            return;
        }

        waveStatusTMP.text = string.Format(
            textWaveProgressFormat,
            current,
            total,
            remainingAfterThis,
            _alive,
            Mathf.Clamp(_currentWaveSpawned, 0, Mathf.Max(0, _currentWaveTarget)),
            Mathf.Max(0, _currentWaveTarget)
        );
    }

    private void SetWaveText(string msg)
    {
        if (!showWaveStatus) return;
        if (waveStatusTMP == null) return;
        waveStatusTMP.text = msg;
    }

    [ContextMenu("Clear Waves")]
    private void ClearWaves()
    {
        waves?.Clear();
        Debug.Log("[WaveManager] Cleared waves list.");
    }
}
