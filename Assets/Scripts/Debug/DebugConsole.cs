using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 인게임 디버그 콘솔.
/// ~ 키로 열고 닫으며, 커맨드를 입력해 게임 상태를 조작한다.
/// 빌드 시 제거하거나 UNITY_EDITOR 심볼로 제한할 수 있다.
/// </summary>
public class DebugConsole : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private int maxLogLines = 20;

    private readonly List<string> logLines = new List<string>();
    private bool isOpen = false;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        // ~ 키로 콘솔 열기/닫기
        if (Keyboard.current != null && Keyboard.current[Key.Backquote].wasPressedThisFrame)
            ToggleConsole();

        // 콘솔이 열려있을 때 Enter로 커맨드 실행
        if (isOpen && Keyboard.current != null && Keyboard.current[Key.Enter].wasPressedThisFrame)
            SubmitCommand();
    }

    private void ToggleConsole()
    {
        isOpen = !isOpen;
        if (panel != null) panel.SetActive(isOpen);

        // 콘솔이 열려있는 동안 게임 입력을 차단해 캐릭터 이동·클릭 등이 발동되지 않게 한다
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(!isOpen);

        if (isOpen && inputField != null)
        {
            inputField.text = "";
            inputField.ActivateInputField();
        }
    }

    private void SubmitCommand()
    {
        if (inputField == null) return;
        string input = inputField.text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        Log($"> {input}");
        ExecuteCommand(input.ToLower());

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void ExecuteCommand(string cmd)
    {
        switch (cmd)
        {
            case "win":
            case "clear":
                CmdClearStage();
                break;

            case "help":
                Log("커맨드 목록:");
                Log("  win / clear  — 현재 스테이지 즉시 클리어");
                Log("  help         — 커맨드 목록 표시");
                break;

            default:
                Log($"알 수 없는 커맨드: {cmd}");
                break;
        }
    }

    // ── 커맨드 구현 ─────────────────────────────────────────────────────

    /// <summary>모든 적 유닛을 즉시 제거해 스테이지 클리어를 트리거한다.</summary>
    private void CmdClearStage()
    {
        List<Unit> enemies = new List<Unit>(UnitManager.Instance.GetEnemyUnitList());
        if (enemies.Count == 0)
        {
            Log("이미 적이 없습니다.");
            return;
        }

        foreach (Unit enemy in enemies)
        {
            HealthSystem hs = enemy.GetComponent<HealthSystem>();
            if (hs != null)
                hs.Damage(999999); // 즉사
        }

        Log($"적 {enemies.Count}명 제거 — 스테이지 클리어!");
    }

    // ── 로그 출력 ────────────────────────────────────────────────────────

    private void Log(string message)
    {
        logLines.Add(message);
        if (logLines.Count > maxLogLines)
            logLines.RemoveAt(0);

        if (logText != null)
            logText.text = string.Join("\n", logLines);
    }
}
