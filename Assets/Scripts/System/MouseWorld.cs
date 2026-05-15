using UnityEngine;

public class MouseWorld : MonoBehaviour
{
    [SerializeField] private LayerMask mousePlaneLayerMask;

    public static MouseWorld Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 로직 정리
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There's more than one MouseWorld! " + transform + " - " + Instance);
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // 필요에 따라 DontDestroyOnLoad 유지 (씬 전환 시 마우스 레이캐스트 유지용)
            // DontDestroyOnLoad(gameObject); 
        }
    }

    public static Vector3 GetPosition()
    {
        // 메인 카메라를 통해 마우스 위치에서 레이(Ray)를 생성
        Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());

        // 레이캐스트 실행
        if (Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, Instance.mousePlaneLayerMask))
        {
            return raycastHit.point;
        }

        // 충돌 지점이 없을 경우 매우 먼 곳(또는 Zero) 반환
        return Vector3.zero;
    }

    // 레이캐스트 히트 정보 전체가 필요한 경우를 위한 추가 메서드 (선택 사항)
    public static RaycastHit GetRaycastHit()
    {
        Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());
        Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, Instance.mousePlaneLayerMask);
        return raycastHit;
    }
}