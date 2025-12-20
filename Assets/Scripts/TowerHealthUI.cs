using UnityEngine;
using UnityEngine.UI;

public class TowerHealthUI : MonoBehaviour
{
    [Header("References")]
    public TowerHealth towerHealth;
    public Slider hpSlider;

    [Tooltip("建議拖 CenterEyeAnchor，讓血條永遠面向玩家")]
    public Transform billboardCamera;

    [Header("Layout")]
    public Vector3 worldOffset = new Vector3(0f, 0.25f, 0f); // 血條在塔上方的偏移
    public bool billboard = true;

    private int lastHp = int.MinValue;
    private int lastMaxHp = int.MinValue;

    private void Reset()
    {
        // 方便：如果你把此腳本掛在 Tower 子物件，可自動抓
        if (!towerHealth) towerHealth = GetComponentInParent<TowerHealth>();
        if (!hpSlider) hpSlider = GetComponentInChildren<Slider>(true);
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
        // 可選：塔死了就把血條關掉（或換成 Game Over 樣式）
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
        // 1) 位置跟著塔（掛在塔底下其實也行；這裡給你更穩的方式）
        if (towerHealth)
            transform.position = towerHealth.transform.position + worldOffset;

        // 2) Billboard：面向玩家
        if (billboard && billboardCamera)
        {
            Vector3 dir = transform.position - billboardCamera.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
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

    private void OnTowerDied()
    {
        // 最低限度：關掉血條；你也可以改成顯示 GAME OVER
        gameObject.SetActive(false);
    }
}
