using System.Collections;
using UnityEngine;

/// <summary>
/// 검 오브젝트에 부착. 점프 중에는 검을 숨기고 착지 후 다시 표시한다.
/// </summary>
public class SwordVisibilityController : MonoBehaviour
{
    [SerializeField] private float showDelay = 0.2f;

    private LeapAction leapAction;

    private void Awake()
    {
        leapAction = GetComponentInParent<LeapAction>();
    }

    private void Start()
    {
        if (leapAction == null) return;
        leapAction.OnLeapStarted += (s, e) => gameObject.SetActive(false);
        // 오브젝트가 비활성화 상태일 때 코루틴을 직접 실행할 수 없으므로 부모에서 실행
        leapAction.OnLeapLanded  += (s, e) => leapAction.StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(showDelay);
        gameObject.SetActive(true);
    }
}
