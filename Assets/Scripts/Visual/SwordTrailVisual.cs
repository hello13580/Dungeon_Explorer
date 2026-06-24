using UnityEngine;

/// <summary>
/// 검 오브젝트에 부착. MeleeAction 공격 중에만 TrailRenderer를 활성화해 검 궤적을 표시한다.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class SwordTrailVisual : MonoBehaviour
{
    private TrailRenderer trail;
    private MeleeAction meleeAction;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;

        meleeAction = GetComponentInParent<MeleeAction>();
        if (meleeAction == null)
            Debug.LogWarning("[SwordTrail] MeleeAction을 찾을 수 없습니다!", this);
    }

    private void Start()
    {
        if (meleeAction == null) return;
        meleeAction.OnSwordActionStarted += (s, e) =>
        {
            Debug.Log("[SwordTrail] 궤적 ON");
            trail.emitting = true;
        };
        meleeAction.OnSwordActionEnded += (s, e) =>
        {
            Debug.Log("[SwordTrail] 궤적 OFF");
            trail.emitting = false;
            trail.Clear();
        };
    }
}
