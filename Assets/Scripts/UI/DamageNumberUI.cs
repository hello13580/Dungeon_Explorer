using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 유닛 월드UI에 붙여두는 피해 숫자 표시기.
/// HealthSystem.OnDamageTaken 이벤트를 받아 숫자를 띄우고 위로 올라가며 사라진다.
/// </summary>
public class DamageNumberUI : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI numberText; // 자식 텍스트 오브젝트 (템플릿)

    [Header("애니메이션")]
    [SerializeField] private float floatSpeed = 15f;
    [SerializeField] private float displayDuration = 0.6f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float xSpread = 8f; // 다단히트 시 X 랜덤 분산 범위

    private void Start()
    {
        if (numberText != null) numberText.gameObject.SetActive(false);
        if (healthSystem != null)
            healthSystem.OnDamageTaken += OnDamageTaken;
    }

    private void OnEnable()
    {
        // [버그 수정] 유닛이 비활성화됐다가 다시 활성화될 때(부활 후 전투 입장 등)
        // 이전에 Instantiate된 피해 숫자 텍스트 오브젝트를 전부 제거한다.
        // 수정 전: 유닛이 피해를 받는 순간 AnimateRoutine 코루틴이 시작되는데,
        //   마지막 피해 직후 SetActive(false)로 유닛이 비활성화되면 코루틴이 중단된 채
        //   생성된 TextMeshPro 인스턴스가 자식 오브젝트로 남는다.
        //   이후 부활해서 SetActive(true)가 호출되면 자식도 함께 활성화되어
        //   이전 스테이지에서 받은 피해 숫자가 멈춰있는 채로 화면에 다시 나타났다.
        if (numberText == null) return;
        foreach (Transform child in transform)
        {
            if (child.gameObject != numberText.gameObject)
                Destroy(child.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
            healthSystem.OnDamageTaken -= OnDamageTaken;
    }

    private void OnDamageTaken(object sender, int damage)
    {
        if (numberText == null) return;

        TextMeshProUGUI text = Instantiate(numberText, transform);
        text.text = damage.ToString();
        // X를 랜덤으로 분산해 다단히트 시 겹치지 않게
        Vector2 basePos = numberText.rectTransform.anchoredPosition;
        text.rectTransform.anchoredPosition = basePos + new Vector2(Random.Range(-xSpread, xSpread), 0f);
        text.gameObject.SetActive(true);

        StartCoroutine(AnimateRoutine(text));
    }

    private IEnumerator AnimateRoutine(TextMeshProUGUI text)
    {
        float elapsed = 0f;
        Vector2 startPos = text.rectTransform.anchoredPosition;
        Color originalColor = text.color;

        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            text.rectTransform.anchoredPosition = startPos + Vector2.up * floatSpeed * elapsed;
            yield return null;
        }

        elapsed = 0f;
        Vector2 floatedPos = text.rectTransform.anchoredPosition;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            text.rectTransform.anchoredPosition = floatedPos + Vector2.up * floatSpeed * 0.5f * elapsed;
            Color c = originalColor;
            c.a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            text.color = c;
            yield return null;
        }

        Destroy(text.gameObject);
    }
}
