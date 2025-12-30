using UnityEngine;
using UnityEngine.UI;

public class TowerHealthUI : MonoBehaviour
{
    [Header("References")]
    public TowerHealth towerHealth;
    public Slider hpSlider;

    [Tooltip("拖 TowerVisual 底下任一個有 Renderer 的物件（建議 tower-round-build-c 的 MeshRenderer），用來計算塔頂位置。")]
    public Renderer boundsSource;

    [Tooltip("血條離塔頂的高度（公尺）。建議 0.02~0.06。")]
    public float topPadding = 0.03f;

    [Tooltip("建議拖 CenterEyeAnchor，讓血條永遠面向玩家")]
    public Transform billboardCamera;

    [Header("Layout")]
    [Tooltip("當 boundsSource 未指定時，才會使用 worldOffset（相對 towerHealth transform）。")]
    public Vector3 worldOffset = new Vector3(0f, 0.25f, 0f);

    public bool billboard = true;

    private int lastHp = int.MinValue;
    private int lastMaxHp = int.MinValue;

    private void Reset()
    {
        // 方便：如果你把此腳本掛在 Tower 子物件，可自動抓
        if (!towerHealth) towerHealth = GetComponentInParent<TowerHealth>();
        if (!hpSlider) hpSlider = GetComponentInChildren<Slider>(true);

        // 嘗試自動找 boundsSource（優先找 TowerVisual 底下的 Renderer）
        if (!boundsSource)
        {
            // 從自己所在物件往下找
            boundsSource = GetComponentInChildren<Renderer>(true);

            // 若掛在 Canvas，Renderer 不在 Canvas 底下，改成找父層（Tower）底下的 Renderer
            if (!boundsSource && transform.parent != null)
                boundsSource = transform.parent.GetComponentInChildren<Renderer>(true);

            // 再不行就找整個 Tower 節點底下
            if (!boundsSource && towerHealth != null)
                boundsSource = towerHealth.GetComponentInChildren<Renderer>(true);
        }
    }

    private void Awake()
    {
        if (!towerHealth) towerHealth = GetComponentInParent<TowerHealth>();
        if (!hpSlider) hpSlider = GetComponentInChildren<Slider>(true);

        if (!towerHealth || !hpSlider)
        {
            Debug.LogError("[TowerHealthUI] 缺少 towerHealth 或 hpSlider，請在 Inspector 指定。");
            enabled = false;
            return;
        }

        // 初始化 Slider 範圍
        ConfigureSliderRange();
        ForceRefresh();
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

    private void LateUpdate()
    {
        // 1) 位置：優先用 boundsSource 的頂端，確保永遠在塔正上方
        if (towerHealth)
        {
            if (boundsSource != null)
            {
                Bounds b = boundsSource.bounds;
                transform.position = new Vector3(b.center.x, b.max.y + topPadding, b.center.z);
            }
            else
            {
                // 沒指定 boundsSource 才退回舊邏輯（可能會因模型高度/pivot 不同而偏）
                transform.position = towerHealth.transform.position + worldOffset;
            }
        }

        // 2) Billboard：面向玩家
        if (billboard && billboardCamera)
        {
            Vector3 dir = transform.position - billboardCamera.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // 3) Hp 有變才刷新 UI
        if (towerHealth && (towerHealth.Hp != lastHp || towerHealth.maxHp != lastMaxHp))
        {
            Refresh();
        }
    }

    private void ConfigureSliderRange()
    {
        hpSlider.minValue = 0;
        hpSlider.maxValue = Mathf.Max(1, towerHealth.maxHp);
    }

    private void Refresh()
    {
        lastHp = towerHealth.Hp;
        lastMaxHp = towerHealth.maxHp;

        // 若 maxHp 在 Inspector 被改動，範圍也要同步
        if (Mathf.Abs(hpSlider.maxValue - lastMaxHp) > 0.001f)
            ConfigureSliderRange();

        hpSlider.value = Mathf.Clamp(lastHp, 0, lastMaxHp);
    }

    private void ForceRefresh()
    {
        lastHp = int.MinValue;
        lastMaxHp = int.MinValue;
        Refresh();
    }

    private void OnTowerDied()
    {
        // 最低限度：關掉血條；你也可以改成顯示 GAME OVER
        gameObject.SetActive(false);
    }
}
