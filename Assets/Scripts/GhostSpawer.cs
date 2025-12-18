using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
public class GhostSpawner : MonoBehaviour
{
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
    public int maxAlive = 25;
    public float lifeTime = 12f;

    [Header("Safety")]
    public bool sanitizeSpawn = true;
    public bool forceTransparentNoDepth = true;
    public bool fallbackSpawnTestPrimitive = false;

    private float _timer;
    private bool _ready;
    private readonly List<GameObject> _alive = new();

    IEnumerator Start()
    {
        while (MRUK.Instance == null || !MRUK.Instance.IsInitialized) yield return null;
        _ready = true;
    }

    void Update()
    {
        if (!_ready) return;

        _timer += Time.deltaTime;
        if (_timer < spawnTimer) return;
        _timer -= spawnTimer;

        _alive.RemoveAll(go => go == null);
        while (_alive.Count >= maxAlive)
        {
            var old = _alive[0];
            _alive.RemoveAt(0);
            if (old) Destroy(old);
        }

        TrySpawnOnce();
    }

    void TrySpawnOnce()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            if (room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,      // ★ 修正：朝上水平面（地板/桌面）
                    minEdgeDistance,
                    LabelFilter.Included(spawnLabels),
                    out var pos, out var norm))
            {
                float pushOut = Mathf.Max(0.01f, Mathf.Abs(normalOffset));

                // 水平面用隨機 Y 旋轉較穩
                var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject go = null;

                if (prefabToSpawn != null)
                {
                    go = Instantiate(prefabToSpawn, pos + norm * pushOut, rot);
                }
                else if (fallbackSpawnTestPrimitive)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetPositionAndRotation(pos + norm * pushOut, rot);
                    go.transform.localScale = Vector3.one * 0.1f;
                    var c = go.GetComponent<Collider>();
                    if (c) Destroy(c);
                }

                if (go != null)
                {
                    // 指派 Tower 給怪（讓怪會往塔走）
                    var enemy = go.GetComponent<GhostEnemy>();
                    if (!enemy) enemy = go.GetComponentInChildren<GhostEnemy>();
                    if (enemy) enemy.tower = tower;

                    if (sanitizeSpawn) SanitizeForPassthrough(go);
                    if (forceTransparentNoDepth) MakeTransparentNoDepth(go);

                    _alive.Add(go);
                    if (lifeTime > 0f) Destroy(go, lifeTime);
                }
                return;
            }
        }
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
