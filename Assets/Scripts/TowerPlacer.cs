using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(50)]
public class TowerPlacer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("強烈建議手動指定：OVRCameraRig/CenterEyeAnchor 或 XR Origin 的 Main Camera。")]
    public Transform cameraTransform;

    [Header("Target Surface")]
    [Tooltip("可用 Flags（例如 TABLE | OTHER）。若桌子沒被標成 TABLE，請把 OTHER 也勾上。")]
    public MRUKAnchor.SceneLabels targetLabels = MRUKAnchor.SceneLabels.TABLE;

    [Tooltip("找不到 targetLabels 時是否退回地板（要強制桌上就關掉）")]
    public bool fallbackToFloor = false;

    [Header("Placement Constraints")]
    [Tooltip("離桌邊至少多遠（越大越不靠邊）")]
    public float minEdgeDistance = 0.1f;

    [Tooltip("把物件往法線方向推離表面，避免穿模（再加上底部對齊）")]
    public float extraSurfaceOffset = 0.01f;

    [Tooltip("只接受距離相機多遠以內的點（越小越不會跑遠）")]
    public float maxDistanceFromCamera = 1.4f;

    [Tooltip("只接受在你面前的點；0.65~0.8 會更集中在正前方")]
    [Range(-1f, 1f)]
    public float minForwardDot = 0.7f;

    [Tooltip("每次放置最多嘗試幾次隨機點（越大越容易找到你面前桌上）")]
    public int attempts = 800;

    [Header("Force 'on desk in front'")]
    [Tooltip("希望塔離你水平距離大約多少（公尺）。例如 0.55~0.75 通常是桌前舒服位置")]
    public float desiredHorizontalDistance = 0.6f;

    [Tooltip("避免選到你正下方的點（水平距離太小會容易落地）")]
    public float minHorizontalDistance = 0.25f;

    [Tooltip("限制表面不能比你的相機低太多（用來排除地板）。站姿可用 0.9~1.1；坐姿 0.7~0.9")]
    public float maxBelowCamera = 0.85f;

    [Header("Startup (重要：避免太早放置造成跑很遠)")]
    public bool placeOnStart = true;

    [Tooltip("等 TrackingOrigin/WorldLock 穩定後再放置（建議 1.0~2.0 秒）")]
    public float warmupSeconds = 1.5f;

    [Tooltip("啟動時自動重試幾次，直到找到你面前桌上的點")]
    public int autoRetries = 12;

    public float retryInterval = 0.25f;

    [Header("Align")]
    [Tooltip("讓塔底部貼齊桌面/地板（解決 Pivot 在中心導致半埋進去）")]
    public bool alignBottomToSurface = true;

#if ENABLE_INPUT_SYSTEM
    [Header("Control (Editor/PC)")]
    public Key rePlaceKeyInputSystem = Key.R;
#endif

    Transform _cam;
    bool _ready;

    IEnumerator Start()
    {
        // 等 MRUK 初始化
        while (MRUK.Instance == null || !MRUK.Instance.IsInitialized)
            yield return null;

        // 等 CurrentRoom 真正出現
        while (MRUK.Instance.GetCurrentRoom() == null)
            yield return null;

        ResolveCamera();

        // 等座標系穩定（避免一開始就 Place，之後 WorldLock 對齊導致「跑很遠」）
        if (warmupSeconds > 0f)
            yield return new WaitForSeconds(warmupSeconds);
        else
            yield return null;

        _ready = true;

        if (placeOnStart)
        {
            for (int i = 0; i < Mathf.Max(1, autoRetries); i++)
            {
                if (TryPlace(out var msg))
                {
                    Debug.Log(msg);
                    break;
                }
                if (retryInterval > 0f) yield return new WaitForSeconds(retryInterval);
                else yield return null;
            }
        }
    }

    void Update()
    {
        if (!_ready) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[rePlaceKeyInputSystem].wasPressedThisFrame)
            Place();
#endif
    }

    void ResolveCamera()
    {
        if (cameraTransform != null)
        {
            _cam = cameraTransform;
            return;
        }

        if (Camera.main != null)
        {
            _cam = Camera.main.transform;
            return;
        }

        var cams = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            if (c == null || !c.enabled) continue;
            if (c.stereoTargetEye != StereoTargetEyeMask.None)
            {
                _cam = c.transform;
                return;
            }
        }

        foreach (var c in cams)
        {
            if (c != null && c.enabled)
            {
                _cam = c.transform;
                return;
            }
        }
    }

    public void Place()
    {
        if (TryPlace(out var msg))
            Debug.Log(msg);
    }

    bool TryPlace(out string debugMsg)
    {
        debugMsg = "";

        ResolveCamera();
        if (_cam == null)
        {
            debugMsg = "[TowerPlacer] No camera found. Please assign cameraTransform (CenterEyeAnchor) in Inspector.";
            Debug.LogWarning(debugMsg);
            return false;
        }

        var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
        if (room == null)
        {
            debugMsg = "[TowerPlacer] CurrentRoom is null (MRUK not ready).";
            Debug.LogWarning(debugMsg);
            return false;
        }

        // 1) 優先桌面（TABLE/OTHER）
        if (TryPickBestOnDeskInFront(room, targetLabels, out var pos, out var norm, out var dist, out var dot, out var hdist))
        {
            ApplyPlacement(pos, norm);
            debugMsg = $"[TowerPlacer] Placed on {targetLabels}  dist={dist:F2}m  horiz={hdist:F2}m  forwardDot={dot:F2}";
            return true;
        }

        // 2) 可選：退回地板
        if (fallbackToFloor)
        {
            if (TryPickBestOnDeskInFront(room, MRUKAnchor.SceneLabels.FLOOR, out pos, out norm, out dist, out dot, out hdist))
            {
                ApplyPlacement(pos, norm);
                debugMsg = $"[TowerPlacer] Fallback placed on FLOOR  dist={dist:F2}m  horiz={hdist:F2}m  forwardDot={dot:F2}";
                return true;
            }
        }

        debugMsg =
            "[TowerPlacer] No valid TABLE-like surface in front. " +
            "Check: (1) 你的桌子是否有被 Space/Scene 掃描進來；(2) targetLabels 是否包含 OTHER；" +
            "(3) maxBelowCamera 太小/太大；(4) minEdgeDistance 太大。";
        Debug.LogWarning(debugMsg);
        return false;
    }

    bool TryPickBestOnDeskInFront(
        MRUKRoom room,
        MRUKAnchor.SceneLabels labels,
        out Vector3 bestPos,
        out Vector3 bestNorm,
        out float bestDist,
        out float bestDot,
        out float bestHorizDist)
    {
        bestPos = default;
        bestNorm = Vector3.up;
        bestDist = float.PositiveInfinity;
        bestDot = -1f;
        bestHorizDist = float.PositiveInfinity;

        var camPos = _cam.position;

        // 用水平 forward 判斷「在面前」
        var camForward = _cam.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 1e-5f) camForward = _cam.forward;
        camForward.Normalize();

        bool found = false;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < Mathf.Max(1, attempts); i++)
        {
            if (!room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,
                    minEdgeDistance,
                    LabelFilter.Included(labels),
                    out var pos,
                    out var norm))
            {
                continue;
            }

            // 只要朝上的面（桌面/地板）
            if (norm.y < 0.6f) continue;

            // 排除地板：表面不能比相機低太多
            float below = camPos.y - pos.y;
            if (below > maxBelowCamera) continue;

            var to = pos - camPos;
            float dist = to.magnitude;
            if (dist > maxDistanceFromCamera) continue;

            // 水平距離 & 在面前
            var flatTo = to; flatTo.y = 0f;
            float horizDist = flatTo.magnitude;
            if (horizDist < minHorizontalDistance) continue;

            float dot = Vector3.Dot(flatTo.normalized, camForward);
            if (dot < minForwardDot) continue;

            // 分數越小越好：越接近「你面前桌上」(水平距離靠近 desired)、越正前方越好
            float score =
                Mathf.Abs(horizDist - desiredHorizontalDistance) * 1.2f +
                (1f - dot) * 0.8f +
                dist * 0.05f;

            if (score < bestScore)
            {
                bestScore = score;
                bestPos = pos;
                bestNorm = norm;
                bestDist = dist;
                bestDot = dot;
                bestHorizDist = horizDist;
                found = true;
            }
        }

        return found;
    }

    void ApplyPlacement(Vector3 pos, Vector3 norm)
    {
        var finalPos = pos + norm * Mathf.Max(0.001f, extraSurfaceOffset);

        // 只轉 Y，讓塔面向你
        var toCam = _cam.position - finalPos;
        toCam.y = 0f;

        var rot = (toCam.sqrMagnitude > 1e-5f)
            ? Quaternion.LookRotation(toCam.normalized, Vector3.up)
            : Quaternion.identity;

        transform.SetPositionAndRotation(finalPos, rot);

        // 再把「塔底部」貼齊桌面（解決 pivot 在中心）
        if (alignBottomToSurface)
        {
            var desiredY = pos.y + Mathf.Max(0.001f, extraSurfaceOffset);
            var bounds = GetWorldBounds(transform);
            if (bounds.size.sqrMagnitude > 1e-6f)
            {
                float delta = desiredY - bounds.min.y;
                transform.position += Vector3.up * delta;
            }
        }
    }

    static Bounds GetWorldBounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs == null || rs.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);
        return b;
    }
}
