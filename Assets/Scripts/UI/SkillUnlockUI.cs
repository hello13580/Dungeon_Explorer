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
    [SerializeField] private GameObject noSkillsMessage;  // 습득 가능한 스킬이 없을 때 표시
    [SerializeField] private UnityEngine.UI.Button continueButton; // 스킬 없을 때 닫기 버튼

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

        bool hasOptions = options != null && options.Count > 0;

        // 선택지 카드 생성
        if (hasOptions)
        {
            foreach (var option in options)
            {
                GameObject cardObj = Instantiate(skillCardPrefab, cardContainer);
                cardObj.GetComponent<SkillCardUI>().Setup(option);
            }
        }

        // 스킬이 없을 때 안내 메시지 및 계속하기 버튼 표시
        if (noSkillsMessage != null) noSkillsMessage.SetActive(!hasOptions);
        if (continueButton != null) continueButton.gameObject.SetActive(!hasOptions);

        panel.SetActive(true);
    }

    /// <summary>계속하기 버튼 클릭 시 호출.</summary>
    public void OnContinueButtonClicked()
    {
        SkillUnlockManager.Instance.SkipUnlock();
    }

    private void OnSkillUnlockCompleted(object sender, System.EventArgs e)
    {
        panel.SetActive(false);
    }
}
