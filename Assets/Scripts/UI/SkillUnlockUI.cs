using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 습득 선택 UI 패널.
/// SkillUnlockManager 이벤트를 구독해 스테이지 클리어 후 자동으로 표시된다.
/// </summary>
public class SkillUnlockUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject skillCardPrefab;

    private void Start()
    {
        panel.SetActive(false);
        SkillUnlockManager.OnSkillUnlockStarted += OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted += OnSkillUnlockCompleted;
    }

    private void OnDestroy()
    {
        SkillUnlockManager.OnSkillUnlockStarted -= OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted -= OnSkillUnlockCompleted;
    }

    private void OnSkillUnlockStarted(object sender, List<SkillUnlockManager.SkillUnlockOption> options)
    {
        // 기존 카드 제거
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        // 선택지 카드 생성
        foreach (var option in options)
        {
            GameObject cardObj = Instantiate(skillCardPrefab, cardContainer);
            cardObj.GetComponent<SkillCardUI>().Setup(option);
        }

        panel.SetActive(true);
    }

    private void OnSkillUnlockCompleted(object sender, System.EventArgs e)
    {
        panel.SetActive(false);
    }
}
