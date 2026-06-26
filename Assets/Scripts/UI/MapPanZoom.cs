using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 맵 패널에 드래그 패닝과 휠 줌 기능을 추가한다.
/// MapPanel 오브젝트에 부착하고 mapContent에 NodeContainer/LineContainer의 부모를 연결한다.
/// </summary>
public class MapPanZoom : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private RectTransform mapContent;

    [Header("줌 설정")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom   = 0.5f;
    [SerializeField] private float maxZoom   = 2.0f;

    [Header("자동 패닝")]
    [SerializeField] private float panSpeed  = 8f;   // 자동 패닝 속도
    [SerializeField] private float panMargin = 200f; // 노드가 화면 왼쪽에서 이만큼 여백을 두고 보임

    private Vector3 _originalScale;
    private Vector3 _targetPosition;
    private Vector3 _currentNodePosition;
    private bool    _autoPanning = false;

    private void Awake()
    {
        _originalScale   = mapContent.localScale;
        _targetPosition  = mapContent.localPosition;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            _targetPosition = _currentNodePosition;
            _autoPanning    = true;
        }

        if (!_autoPanning) return;
        mapContent.localPosition = Vector3.Lerp(
            mapContent.localPosition, _targetPosition, Time.deltaTime * panSpeed);

        if (Vector3.Distance(mapContent.localPosition, _targetPosition) < 0.5f)
        {
            mapContent.localPosition = _targetPosition;
            _autoPanning = false;
        }
    }

    /// <summary>맵이 열릴 때 위치/줌 초기화. nodeX를 지정하면 해당 위치가 왼쪽 여백 기준으로 보임.</summary>
    public void ResetView(float nodeX = 0f)
    {
        _currentNodePosition     = new Vector3(-nodeX + panMargin, 0f, 0f);
        _targetPosition          = _currentNodePosition;
        mapContent.localPosition = _targetPosition;
        mapContent.localScale    = _originalScale;
        _autoPanning             = false;
    }

    /// <summary>현재 위치 버튼 클릭 시 호출. 백스페이스와 동일한 동작.</summary>
    public void OnReturnToCurrentNodeClicked()
    {
        _targetPosition = _currentNodePosition;
        _autoPanning    = true;
    }

    /// <summary>현재 노드 위치로 부드럽게 패닝. nodeX는 mapContent 로컬 기준 픽셀 X.</summary>
    public void PanToNode(float nodeX)
    {
        _currentNodePosition = new Vector3(-nodeX + panMargin, 0f, 0f);
        _targetPosition      = _currentNodePosition;
        _autoPanning         = true;
    }

    // ─── 드래그 패닝 ─────────────────────────────────────────────

    public void OnDrag(PointerEventData eventData)
    {
        _autoPanning = false; // 드래그 중엔 자동 패닝 중단
        mapContent.localPosition += new Vector3(eventData.delta.x, eventData.delta.y, 0f);
        _targetPosition = mapContent.localPosition;
    }

    // ─── 휠 줌 ───────────────────────────────────────────────────

    public void OnScroll(PointerEventData eventData)
    {
        float scroll      = eventData.scrollDelta.y;
        float currentZoom = mapContent.localScale.x;
        float newZoom     = Mathf.Clamp(currentZoom + scroll * zoomSpeed, minZoom, maxZoom);
        mapContent.localScale = Vector3.one * newZoom;
    }
}
