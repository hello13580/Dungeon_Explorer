using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자원(AP·마나·JP) 부족 시 화면에 안내 메시지를 잠깐 표시한다.
/// Canvas 아래 오브젝트에 붙인 뒤 Inspector에서 아이콘·텍스트를 연결하면 된다.
/// </summary>
public class ResourceNotificationUI : MonoBehaviour
{
    public static ResourceNotificationUI Instance { get; private set; }

    [Header("UI 레퍼런스")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("아이콘 (AP·JP 부족 시 표시)")]
    [SerializeField] private Image apIconImage;
    [SerializeField] private Image jpIconImage;

    [Header("표시 설정")]
    [SerializeField] private float displayDuration = 1.8f;
    [SerializeField] private float fadeDuration    = 0.4f;

    private Coroutine activeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 전체 투명도만 0으로 → 아이콘 SetActive는 건드리지 않음
        // (Awake에서 SetActive(false)를 호출하면 외부 오브젝트가 연결됐을 때 그걸 꺼버리는 버그 발생)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // 알림은 클릭을 받을 필요 없으므로 항상 통과
        }
    }

    /// <summary>
    /// 자원 부족 안내를 표시한다.
    /// UnitActionSystem이 유효한 타일 클릭이지만 자원이 부족할 때 호출한다.
    /// </summary>
    public void ShowResourceShortage(bool lackAP, bool lackMana, bool lackJP)
    {
        if (!lackAP && !lackMana && !lackJP) return;

        // 부족 자원 이름 목록 조합
        List<string> names = new List<string>();
        if (lackAP)   names.Add("행동 포인트");
        if (lackMana) names.Add("마나");
        if (lackJP)   names.Add("직업 포인트");

        string joined = string.Join(", ", names);
        messageText.text = $"{joined}가 부족합니다";

        // gameObject.SetActive 대신 Image.enabled 사용
        // → SetActive는 프리팹 오버라이드로 저장될 수 있어서 프리팹이 꺼진 채 저장되는 버그 발생
        if (apIconImage != null) apIconImage.enabled = lackAP;
        if (jpIconImage != null) jpIconImage.enabled = lackJP;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // 즉시 불투명
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        // 서서히 사라짐
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        activeRoutine = null;
    }
}
