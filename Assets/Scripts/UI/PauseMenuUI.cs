using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC 키로 열고 닫는 일시정지 메뉴.
/// "게임 재개" 버튼 → 패널 닫기
/// "게임 종료" 버튼 → 애플리케이션 종료
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;

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
