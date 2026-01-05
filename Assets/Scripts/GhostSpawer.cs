using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
public class GhostSpawner : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("true=依 spawnTimer 自動生成（demo模式）；false=由 WaveManager 呼叫 TrySpawn 控制生成。")]
    public bool autoSpawn = true;

    [Header("Spawn")]
    public float spawnTimer = 1f;
    public GameObject prefabToSpawn;
    public int attemptsPerSpawn = 24;

    [Header("Placement")]
    public float minEdgeDistance = 0.3f;

    // 預設在地板生成；若你要桌面請在 Inspector 改成 TABLE（若有）
    public MRUKAnchor.SceneLabels spawnLabels = MRUKAnchor.SceneLabels.FLOOR;

    // 推離表面一點點（生成在桌面/地板上方），避免穿模
    public float normalOffset = 0.04f;

    [Header("Game")]
    public Transform tower;

    [Header("Lifetime / Pooling")]
    [Tooltip("僅建議在 autoSpawn=true 時用於限制場上數量；Wave 模式請交給 WaveManager 控制並把 autoSpawn 關掉。")]
    public int maxAlive = 25;

    [Tooltip("Wave 模式建議設為 0，避免時間到 Destroy 造成波次計數干擾。")]
    public float lifeTime = 12f;

    [Header("Safety")]
    public bool sanitizeSpawn = true;
    public bool forceTransparentNoDepth = true;
    public bool fallbackSpawnTestPrimitive = false;

    private float _timer;
    private bool _ready;
    private readonly List<GameObject> _alive = new();

    public int AliveCount
    {
        get
        {
            _alive.RemoveAll(go => go == null);
            return _alive.Count;
        }
    }

    IEnumerator Start()
    {
        while (MRUK.Instance == null || !MRUK.Instance.IsInitialized) yield return null;
        _ready = true;
    }

    void Update()
    {
        if (!_ready) return;
        if (!autoSpawn) return;

        _timer += Time.deltaTime;
        if (_timer < spawnTimer) return;
        _timer -= spawnTimer;

        _alive.RemoveAll(go => go == null);

        // 自動模式下才做 maxAlive 的踢除，避免 Wave 模式被干擾
        while (_alive.Count >= maxAlive)
        {
            var old = _alive[0];
            _alive.RemoveAt(0);
            if (old) Destroy(old);
        }

        // 自動模式：用預設 prefabToSpawn
        TrySpawn(prefabToSpawn, out _);
    }

    /// <summary>
    /// WaveManager 會呼叫這個方法來生成指定 prefab。
    /// prefabOverride 若為 null，會 fallback 用 prefabToSpawn。
    /// 成功會回傳 true，並輸出 enemy（可能為 null：若 prefab 沒有 GhostEnemy）。
    /// </summary>
    public bool TrySpawn(GameObject prefabOverride, out GhostEnemy enemy)
    {
        enemy = null;

        // 重要：即使此 component 被 disable，其他腳本仍可直接呼叫 TrySpawn。
        // GameOver 後 GameManager 會把 spawner.enabled=false，此處必須保底擋住。
        if (!enabled || !gameObject.activeInHierarchy) return false;

        if (!_ready) return false;

        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return false;

        var prefab = prefabOverride != null ? prefabOverride : prefabToSpawn;
        if (prefab == null && !fallbackSpawnTestPrimitive) return false;

        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            if (room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,      // 朝上水平面（地板/桌面）
                    minEdgeDistance,
                    LabelFilter.Included(spawnLabels),
                    out var pos, out var norm))
            {
                float pushOut = Mathf.Max(0.01f, Mathf.Abs(normalOffset));

                // 水平面用隨機 Y 旋轉較穩
                var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject go = null;

                if (prefab != null)
                {
                    go = Instantiate(prefab, pos + norm * pushOut, rot);
                }
                else if (fallbackSpawnTestPrimitive)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetPositionAndRotation(pos + norm * pushOut, rot);
                    go.transform.localScale = Vector3.one * 0.1f;
                    var c = go.GetComponent<Collider>();
                    if (c) Destroy(c);
                }

                if (go == null) return false;

                // 指派 Tower 給怪（讓怪會往塔走）
                var e = go.GetComponent<GhostEnemy>();
                if (!e) e = go.GetComponentInChildren<GhostEnemy>();
                if (e) e.tower = tower;

                if (sanitizeSpawn) SanitizeForPassthrough(go);
                if (forceTransparentNoDepth) MakeTransparentNoDepth(go);

                _alive.Add(go);

                // Wave 模式建議把 lifeTime 設 0；若仍要用自動回收，GhostEnemy 的 OnDestroy 保底要能觸發 Died
                if (lifeTime > 0f) Destroy(go, lifeTime);

                enemy = e;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清空目前 Spawner 追蹤的物件（重開/除錯很有用）
    /// </summary>
    public void DespawnAll()
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            var go = _alive[i];
            if (go) Destroy(go);
        }
        _alive.Clear();
    }

    void SanitizeForPassthrough(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
        {
            Debug.LogWarning($"[GhostSpawner] Disabled child Camera on {cam.name}");
            cam.enabled = false;
        }

        foreach (var sky in root.GetComponentsInChildren<Skybox>(true))
        {
            Debug.LogWarning($"[GhostSpawner] Removed Skybox on {sky.name}");
            Destroy(sky);
        }

#if OCULUS_INTEGRATION_PRESENT
        foreach (var ov in root.GetComponentsInChildren<OVROverlay>(true))
        {
            Debug.LogWarning($"[GhostSpawner] Removed OVROverlay on {ov.name}");
            Destroy(ov);
        }
#endif

        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning($"[GhostSpawner] Canvas {canvas.name} set to ScreenSpaceCamera");
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
            }
        }

        if (root.transform.lossyScale.magnitude > 50f)
            root.transform.localScale = Vector3.one;
    }

    void MakeTransparentNoDepth(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                m.renderQueue = (int)RenderQueue.Transparent;
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
                if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)CullMode.Back);

                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }
        }
    }
}
