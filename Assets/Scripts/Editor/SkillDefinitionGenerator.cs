using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 메뉴 Tools → Create Missing Skill Definitions 실행 시
/// Assets/Skills/ 폴더에 아직 없는 SkillDefinition asset을 자동 생성한다.
/// 이미 존재하는 파일은 건드리지 않는다.
/// </summary>
public static class SkillDefinitionGenerator
{
    private const string OutputFolder = "Assets/Skills";

    // (파일명, skillName, actionTypeName) 순서
    private static readonly (string file, string skillName, string actionTypeName)[] Definitions =
    {
        ("AttackAuraAction",  "공격력 오라",  "AttackAuraAction"),
        ("DefenseAction",     "방어",         "DefenseAction"),
        ("HealAction",        "치유",         "HealAction"),
        ("SpinAction",        "회전 공격",    "SpinAction"),
        ("SprintAction",      "질주",         "SprintAction"),
        ("AOEAction",         "수류탄",       "AOEAction"),
        ("WhirlwindAction",   "휠윈드",       "WhirlwindAction"),
        ("LeapAction",        "도약",         "LeapAction"),
        ("SelfHealAction",    "자가 치유",    "SelfHealAction"),
    };

    [MenuItem("Tools/Create Missing Skill Definitions")]
    public static void CreateMissingSkillDefinitions()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Skills");
        }

        int created = 0;
        foreach (var (file, skillName, actionTypeName) in Definitions)
        {
            string path = $"{OutputFolder}/{file}.asset";

            // 이미 있으면 건너뜀
            if (File.Exists(Path.Combine(Application.dataPath, $"Skills/{file}.asset")))
            {
                Debug.Log($"[SkillDefinitionGenerator] 이미 존재함, 건너뜀: {file}.asset");
                continue;
            }

            SkillDefinition def = ScriptableObject.CreateInstance<SkillDefinition>();
            def.skillName      = skillName;
            def.description    = "";          // GetDescription()으로 자동 반영되므로 비워둠
            def.actionTypeName = actionTypeName;

            AssetDatabase.CreateAsset(def, path);
            created++;
            Debug.Log($"[SkillDefinitionGenerator] 생성 완료: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Skill Definition Generator",
            created > 0 ? $"{created}개 생성 완료!\nAssets/Skills/ 폴더를 확인하세요." : "새로 생성할 SkillDefinition이 없습니다.",
            "확인"
        );
    }
}
