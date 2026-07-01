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
        if (Keyboard.current != null && Keyboard.current[Key.Backquote].wasPressedThisFrame)
            ToggleConsole();

        if (isOpen && Keyboard.current != null && Keyboard.current[Key.Enter].wasPressedThisFrame)
            SubmitCommand();
    }

    private void ToggleConsole()
    {
        isOpen = !isOpen;
        if (panel != null) panel.SetActive(isOpen);

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
        ExecuteCommand(input);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void ExecuteCommand(string raw)
    {
        // 커맨드와 인자를 공백 기준으로 분리
        string[] parts = raw.Trim().Split(' ');
        string cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "win":
            case "clear":
                CmdClearStage();
                break;

            case "help":
                Log("커맨드 목록:");
                Log("  win / clear            — 현재 스테이지 즉시 클리어");
                Log("  ap <값>                — 모든 아군 행동 포인트 추가");
                Log("  hp <값>                — 모든 아군 체력 회복");
                Log("  mana <값>              — 모든 아군 마나 설정");
                Log("  jp <값>                — 모든 아군 직업 포인트 추가");
                Log("  atk <값>               — 모든 아군 공격력 영구 증가");
                Log("  def <값>               — 모든 아군 방어력 영구 증가");
                Log("  manaregen <값>         — 모든 아군 마나 재생력 영구 증가");
                Log("  help                   — 커맨드 목록 표시");
                break;

            case "ap":
                CmdAddActionPoint(parts);
                break;

            case "hp":
                CmdHeal(parts);
                break;

            case "mana":
                CmdSetMana(parts);
                break;

            case "jp":
                CmdAddJobPoint(parts);
                break;

            case "atk":
                CmdAddAttack(parts);
                break;

            case "def":
                CmdAddDefense(parts);
                break;

            case "manaregen":
                CmdAddManaRegen(parts);
                break;

            default:
                Log($"알 수 없는 커맨드: {cmd}  (help 입력시 목록 표시)");
                break;
        }
    }

    // ── 커맨드 구현 ─────────────────────────────────────────────────────

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
            if (hs != null) hs.Damage(999999);
        }
        Log($"적 {enemies.Count}명 제거 — 스테이지 클리어!");
    }

    private void CmdAddActionPoint(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
            unit.RestoreActionPoints(val);
        Log($"아군 전원 행동 포인트 +{val}");
    }

    private void CmdHeal(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
            unit.Heal(val);
        Log($"아군 전원 체력 +{val}");
    }

    private void CmdSetMana(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        int count = 0;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            ManaSystem ms = unit.GetManaSystem();
            if (ms == null) continue;
            ms.SetMana(val);
            count++;
        }
        Log($"마나 시스템 보유 아군 {count}명 마나 → {val}");
    }

    private void CmdAddJobPoint(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        int count = 0;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            JobPointSystem jps = unit.GetJobPointSystem();
            if (jps == null) continue;
            jps.AddJobPoints(val);
            count++;
        }
        Log($"직업 포인트 보유 아군 {count}명에게 JP +{val}");
    }

    private void CmdAddAttack(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
            unit.AddPermanentAttackPower(val);
        Log($"아군 전원 공격력 +{val}");
    }

    private void CmdAddDefense(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
            unit.AddPermanentDefensePower(val);
        Log($"아군 전원 방어력 +{val}");
    }

    private void CmdAddManaRegen(string[] parts)
    {
        if (!TryParseInt(parts, out int val)) return;
        int count = 0;
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            ManaSystem ms = unit.GetManaSystem();
            if (ms == null) continue;
            ms.AddBonusRegen(val);
            count++;
        }
        Log($"마나 시스템 보유 아군 {count}명에게 마나 재생력 +{val}");
    }

    // ── 유틸 ────────────────────────────────────────────────────────────

    private bool TryParseInt(string[] parts, out int val)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out val))
        {
            Log("숫자 인자가 필요합니다. 예: hp 50");
            val = 0;
            return false;
        }
        return true;
    }

    private void Log(string message)
    {
        logLines.Add(message);
        if (logLines.Count > maxLogLines)
            logLines.RemoveAt(0);
        if (logText != null)
            logText.text = string.Join("\n", logLines);
    }
}
