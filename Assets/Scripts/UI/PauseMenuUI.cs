using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC 키로 열고 닫는 일시정지 메뉴.
/// "게임 재개" 버튼 → 패널 닫기
/// "메인 메뉴" 버튼 → 세이브 후 메인 메뉴로 복귀
/// "게임 종료" 버튼 → 애플리케이션 종료
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private MainMenuUI mainMenuUI;

    private bool isOpen = false;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private void Open()
    {
        isOpen = true;
        panel.SetActive(true);
        Time.timeScale = 0f; // 게임 일시정지

        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(false);
    }

    private void Close()
    {
        isOpen = false;
        panel.SetActive(false);
        Time.timeScale = 1f; // 게임 재개

        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(true);
    }

    // ── 버튼 연결 ────────────────────────────────────────────────────────

    /// <summary>게임 재개 버튼 OnClick에 연결</summary>
    public void OnResumeClicked()
    {
        Close();
    }

    /// <summary>
    /// 메인 메뉴 버튼 OnClick에 연결.
    /// 현재 런 상태를 저장한 뒤 메인 메뉴로 돌아간다.
    /// 전투 중 종료이므로 IsCurrentNodeCleared = false → 컨티뉴 시 해당 스테이지부터 재시작.
    /// </summary>
    public void OnMainMenuClicked()
    {
        // 현재 상태 저장 (전투 중이므로 OnNodeVisited 저장과 동일한 방식으로 처리됨)
        SaveSystem.Instance?.Save();

        // 시간·입력을 정상화한 뒤 메인 메뉴 표시
        Close();
        mainMenuUI?.Show();
    }

    /// <summary>게임 종료 버튼 OnClick에 연결</summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
