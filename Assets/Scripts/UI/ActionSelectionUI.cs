using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 범용 인게임 선택 패널.
/// 어떤 액션이든 OnSelectionRequested 이벤트로 선택지를 요청하면 패널이 열린다.
/// 플레이어가 선택하면 콜백으로 선택한 인덱스를 반환한다.
/// </summary>
public class ActionSelectionUI : MonoBehaviour
{
    /// <summary>선택 요청 데이터. 선택지 목록과 결과 콜백을 담는다.</summary>
    public class SelectionRequest
    {
        public string title;
        public List<OptionData> options;
        public Action<int> onSelected; // 선택한 인덱스 반환. 취소 시 -1.

        public SelectionRequest(string title, List<OptionData> options, Action<int> onSelected)
        {
            this.title = title;
            this.options = options;
            this.onSelected = onSelected;
        }
    }

    public class OptionData
    {
        public string name;
        public string description;

        public OptionData(string name, string description)
        {
            this.name = name;
            this.description = description;
        }
    }

    /// <summary>선택 패널을 열도록 요청하는 전역 이벤트.</summary>
    public static event EventHandler<SelectionRequest> OnSelectionRequested;

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform optionContainer;   // 옵션 버튼들의 부모
    [SerializeField] private GameObject optionButtonPrefab; // OptionButtonUI 프리팹

    private Action<int> pendingCallback;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private void Start()
    {
        panel.SetActive(false);
        OnSelectionRequested += HandleSelectionRequested;
    }

    private void OnDestroy()
    {
        OnSelectionRequested -= HandleSelectionRequested;
    }

    private void HandleSelectionRequested(object sender, SelectionRequest request)
    {
        // 기존 버튼 정리
        foreach (GameObject btn in spawnedButtons)
            Destroy(btn);
        spawnedButtons.Clear();

        pendingCallback = request.onSelected;

        if (titleText != null)
            titleText.text = request.title;

        // 선택지 버튼 생성
        for (int i = 0; i < request.options.Count; i++)
        {
            int index = i; // 클로저 캡처용
            OptionData option = request.options[i];

            GameObject btnObj = Instantiate(optionButtonPrefab, optionContainer);
            spawnedButtons.Add(btnObj);

            OptionButtonUI btnUI = btnObj.GetComponent<OptionButtonUI>();
            if (btnUI != null)
            {
                btnUI.Setup(option.name, option.description, () => SelectOption(index));
            }
        }

        panel.SetActive(true);
    }

    private void SelectOption(int index)
    {
        panel.SetActive(false);
        pendingCallback?.Invoke(index);
        pendingCallback = null;
    }

    /// <summary>외부에서 선택 요청을 보내는 편의 메서드.</summary>
    public static void RequestSelection(string title, List<OptionData> options, Action<int> onSelected)
    {
        OnSelectionRequested?.Invoke(null, new SelectionRequest(title, options, onSelected));
    }
}
