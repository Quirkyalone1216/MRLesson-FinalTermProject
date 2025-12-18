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
    public MRUKAnchor.SceneLabels spawnLabels = MRUKAnchor.SceneLabels.WALL_FACE;
    public float normalOffset = 0.06f;

    [Header("Lifetime / Pooling")]
    public int maxAlive = 25;
    public float lifeTime = 12f;

    [Header("Safety")]
    public bool sanitizeSpawn = true;              // ★ 1. 啟用生成物件消毒
    public bool forceTransparentNoDepth = true;    // ★ 2. 透明+不寫深度
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
        while (_alive.Count >= maxAlive) { var old = _alive[0]; _alive.RemoveAt(0); if (old) Destroy(old); }

        TrySpawnOnce();
    }

    void TrySpawnOnce()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            if (room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.VERTICAL,
                    minEdgeDistance,
                    LabelFilter.Included(spawnLabels),
                    out var pos, out var norm))
            {
                float pushOut = Mathf.Max(0.01f, Mathf.Abs(normalOffset));
                var rot = Quaternion.LookRotation(-norm, Vector3.up);

                GameObject go = null;
                if (prefabToSpawn != null)
                    go = Instantiate(prefabToSpawn, pos + norm * pushOut, rot);
                else if (fallbackSpawnTestPrimitive)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetPositionAndRotation(pos + norm * pushOut, rot);
                    go.transform.localScale = Vector3.one * 0.1f;
                    var c = go.GetComponent<Collider>(); if (c) Destroy(c);
                }

                if (go != null)
                {
                    if (sanitizeSpawn) SanitizeForPassthrough(go);      // ★ 核心：消毒
                    if (forceTransparentNoDepth) MakeTransparentNoDepth(go);

                    _alive.Add(go);
                    if (lifeTime > 0f) Destroy(go, lifeTime);
                }
                return;
            }
        }
    }

    // —— 移除會破壞合成的東西：子相機、Skybox、Overlay、全螢幕 Canvas 等 ——
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
        // 防呆：若生成物件尺寸極大，縮回來避免鋪滿視野
        if (root.transform.lossyScale.magnitude > 50f) root.transform.localScale = Vector3.one;
    }

    // —— 把所有 Renderer 的材質切成透明、不寫深度、無陰影 ——
    void MakeTransparentNoDepth(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            var mats = r.materials; // 實例化材質
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (!m) continue;
                m.renderQueue = (int)RenderQueue.Transparent;  // 3000+
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);  // 1=Transparent
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
