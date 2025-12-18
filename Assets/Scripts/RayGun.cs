using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RayGun : MonoBehaviour
{
    [Header("Mask / Input / Visuals")]
    public LayerMask layerMask;                                // 要命中的層
    public OVRInput.RawButton shootingButton = OVRInput.RawButton.RIndexTrigger;
    public LineRenderer linePrefab;

    [Header("Muzzle")]
    public Transform shootingPoint;                            // 槍口 (Z+ 向前)
    public float maxLineDistance = 5f;
    public float lineShowTimer = 0.3f;

    [Header("Audio")]
    public AudioSource source;                                 // 取消 Play On Awake
    public AudioClip shootingAudioClip;

    [Header("Impact VFX")]
    public GameObject rayImpactPrefab;                         // 命中特效/貼花
    public float impactTTL = 1.0f;                             // 幾秒後自毀

    void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (OVRInput.GetDown(shootingButton))
            Shoot();
    }

    public void Shoot()
    {
        // 音效（影片同款）
        if (shootingAudioClip && source)
            source.PlayOneShot(shootingAudioClip);

        // 由槍口沿 forward 打射線
        Ray ray = new Ray(shootingPoint.position, shootingPoint.forward);

        bool hasHit = Physics.Raycast(
            ray, out RaycastHit hit, maxLineDistance, layerMask,
            QueryTriggerInteraction.Ignore
        );

        // 命中就用 hit.point；否則固定最遠距離
        Vector3 endPoint = hasHit ? hit.point : ray.origin + ray.direction * maxLineDistance;

        // 命中時生成 impact，旋轉對齊「表面法線的反向」（讓特效朝外）
        if (hasHit && rayImpactPrefab)
        {
            Quaternion rayImpactRotation = Quaternion.LookRotation(-hit.normal);
            GameObject rayImpact = Instantiate(rayImpactPrefab, hit.point, rayImpactRotation);
            if (impactTTL > 0f) Destroy(rayImpact, impactTTL);
        }

        // 畫線（世界座標；兩點即可）
        LineRenderer line = Instantiate(linePrefab);
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, shootingPoint.position);
        line.SetPosition(1, endPoint);
        Destroy(line.gameObject, lineShowTimer);

        // （可選）在 Console 觀察命中資訊
        if (hasHit)
            Debug.Log($"[RayGun] Hit {hit.collider.name} @ {hit.distance:F2}m on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
    }
}
