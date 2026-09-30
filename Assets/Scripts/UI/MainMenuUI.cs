using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 시작 시 가장 먼저 표시되는 메인 메뉴.
/// 처음부터하기 / 이어하기 / 게임종료 세 가지 선택지를 제공한다.
///
/// Inspector 설정:
///   - panel            : 메인 메뉴 루트 GameObject
///   - continueButton   : 세이브가 없으면 비활성화됨
///   - characterSelectUI: "처음부터하기" 선택 시 표시할 캐릭터 선택 화면
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panel;

    [Header("버튼")]
    [SerializeField] private Button continueButton;

    [Header("참조")]
    [SerializeField] private CharacterSelectUI characterSelectUI;

    private void Start()
    {
        // 세이브 파일 유무에 따라 이어하기 버튼 활성화 여부 결정
        RefreshContinueButton();
        Show();
    }

    // ─── 버튼 이벤트 ──────────────────────────────────────────────

    /// <summary>처음부터하기: 메인 메뉴를 닫고 캐릭터 선택 화면을 연다.</summary>
    public void OnNewGameClicked()
    {
        Hide();
        characterSelectUI.ShowFromMainMenu(onBack: Show); // 뒤로가기 시 메인 메뉴 복귀
    }

    /// <summary>
    /// 이어하기: 세이브를 복원하고 맵 화면으로 바로 이동한다.
    /// 세이브 파일이 없으면 버튼 자체가 비활성화되므로 이 메서드는 호출되지 않는다.
    /// </summary>
    public void OnContinueClicked()
    {
        Hide();
        CharacterSelectManager.Instance.ContinueGame();
    }

    /// <summary>게임종료: 에디터에서는 플레이 모드를 종료하고, 빌드에서는 프로세스를 닫는다.</summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── 표시 제어 ────────────────────────────────────────────────

    public void Show()
    {
        RefreshContinueButton();
        panel?.SetActive(true);
    }

    private void Hide() => panel?.SetActive(false);

    /// <summary>세이브 파일 유무에 따라 이어하기 버튼 활성화 여부를 갱신한다.</summary>
    private void RefreshContinueButton()
    {
        bool hasSave = SaveSystem.Instance != null && SaveSystem.Instance.HasSave();
        if (continueButton != null)
            continueButton.interactable = hasSave;
    }
}
