using UnityEngine;
using UnityEngine.UI;

public class GhostHealthUI : MonoBehaviour
{
    [Header("References")]
    public GhostEnemy ghost;
    public Slider hpSlider;

    [Tooltip("建議留空；執行時會自動抓 OVRCameraRig.centerEyeAnchor 或 Camera.main。Prefab 內不要引用 Scene 物件。")]
    public Transform billboardCamera;

    [Header("Layout")]
    [Tooltip("建議只做微調（x/z 左右前後微移）。高度主要由 extraHeight 控制。")]
    public Vector3 worldOffset = new Vector3(0f, 0f, 0f);

    [Tooltip("在模型頭頂中心點再往上加的高度（最常調這個）。")]
    public float extraHeight = 0.05f;

    public bool billboard = true;

    [Header("Anchor")]
    [Tooltip("勾選：以 Renderer bounds 的 top-center 當血條錨點（推薦，避免模型 Pivot 偏移）。")]
    public bool useRendererBoundsAnchor = true;

    private int lastHp = int.MinValue;
    private int lastMaxHp = int.MinValue;

    private Renderer[] cachedRenderers;

    private void Reset()
    {
        if (!ghost) ghost = GetComponentInParent<GhostEnemy>();
        if (!hpSlider) hpSlider = GetComponentInChildren<Slider>(true);
    }

    private void Awake()
    {
        if (!ghost) ghost = GetComponentInParent<GhostEnemy>();
        if (!hpSlider) hpSlider = GetComponentInChildren<Slider>(true);

        if (!ghost || !hpSlider)
        {
            Debug.LogError("[GhostHealthUI] 缺少 ghost 或 hpSlider，請在 Inspector 指定。");
            enabled = false;
            return;
        }

        // Cache renderers once (for bounds anchor)
        cachedRenderers = ghost.GetComponentsInChildren<Renderer>(true);

        // Auto-find camera (prefer OVRCameraRig)
        if (!billboardCamera)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig && rig.centerEyeAnchor) billboardCamera = rig.centerEyeAnchor;
            else if (Camera.main) billboardCamera = Camera.main.transform;
        }

        ConfigureSliderRange();
        ForceRefresh();
    }

    private void OnEnable()
    {
        if (ghost != null)
            ghost.Died += OnGhostDied;
    }

    private void OnDisable()
    {
        if (ghost != null)
            ghost.Died -= OnGhostDied;
    }

    private void LateUpdate()
    {
        if (!ghost)
        {
            Destroy(gameObject); // 避免殘留 UI
            return;
        }

        // 位置：用 top-center anchor（避免 pivot 偏移）
        Vector3 anchor = useRendererBoundsAnchor ? GetTopCenterWorld() : ghost.transform.position;
        transform.position = anchor + worldOffset + Vector3.up * extraHeight;

        // Billboard：面向玩家
        if (billboard && billboardCamera)
        {
            Vector3 dir = transform.position - billboardCamera.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // Hp 有變才刷新
        if (ghost.Hp != lastHp || ghost.maxHp != lastMaxHp)
            Refresh();
    }

    private Vector3 GetTopCenterWorld()
    {
        // 如果沒有 renderer，退回用 transform
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            return ghost.transform.position;

        // 有些 renderer 可能在 runtime 被 disable，仍可用 bounds
        Bounds b = cachedRenderers[0].bounds;
        for (int i = 1; i < cachedRenderers.Length; i++)
            b.Encapsulate(cachedRenderers[i].bounds);

        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    private void ConfigureSliderRange()
    {
        hpSlider.minValue = 0;
        hpSlider.maxValue = Mathf.Max(1, ghost.maxHp);
    }

    private void Refresh()
    {
        lastHp = ghost.Hp;
        lastMaxHp = ghost.maxHp;

        if (hpSlider.maxValue != lastMaxHp)
            ConfigureSliderRange();

        hpSlider.value = Mathf.Clamp(lastHp, 0, lastMaxHp);
    }

    private void ForceRefresh()
    {
        lastHp = int.MinValue;
        lastMaxHp = int.MinValue;
        Refresh();
    }

    private void OnGhostDied()
    {
        gameObject.SetActive(false);
    }
}
