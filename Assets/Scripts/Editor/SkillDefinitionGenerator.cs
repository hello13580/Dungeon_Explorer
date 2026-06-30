using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// 메뉴 Tools → Create Missing Skill Definitions 실행 시
/// 프로젝트에 존재하는 모든 BaseAction 서브클래스를 스캔하여,
/// 아직 SkillDefinition 에셋이 없는 액션에 대해 Assets/Skills/ 폴더에 자동 생성한다.
/// 이미 SkillDefinition이 있는 액션(actionTypeName 기준, 프로젝트 어디에 있든)은 건드리지 않는다.
/// </summary>
public static class SkillDefinitionGenerator
{
    private const string OutputFolder = "Assets/Skills";

    [MenuItem("Tools/Create Missing Skill Definitions")]
    public static void CreateMissingSkillDefinitions()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Skills");
        }

        // 1. 로드된 모든 어셈블리에서 BaseAction의 구체 서브클래스를 전부 수집
        List<Type> actionTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetTypesSafe)
            .Where(t => t != null && typeof(BaseAction).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        // 2. 프로젝트 전체에서 이미 존재하는 SkillDefinition의 actionTypeName 수집
        //    (Assets/Skills 폴더가 아니더라도 어딘가에 이미 있으면 중복 생성하지 않는다)
        var existingTypeNames = new HashSet<string>(
            AssetDatabase.FindAssets("t:SkillDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(def => def != null && !string.IsNullOrEmpty(def.actionTypeName))
                .Select(def => def.actionTypeName)
        );

        int created = 0;
        foreach (Type actionType in actionTypes)
        {
            string typeName = actionType.Name;
            if (existingTypeNames.Contains(typeName))
            {
                continue;
            }

            string path = $"{OutputFolder}/{typeName}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkillDefinition>(path) != null)
            {
                continue; // 동일 경로에 이미 존재 (안전망)
            }

            string skillName = GetDefaultActionName(actionType);
            if (string.IsNullOrEmpty(skillName))
            {
                // DefaultActionName()이 없거나 빈 문자열이면 클래스명에서 유추
                string baseName = typeName.EndsWith("Action") ? typeName.Substring(0, typeName.Length - "Action".Length) : typeName;
                skillName = ObjectNames.NicifyVariableName(baseName);
            }

            SkillDefinition def = ScriptableObject.CreateInstance<SkillDefinition>();
            def.skillName      = skillName;
            def.description    = "";          // GetDescription()으로 자동 반영되므로 비워둠
            def.actionTypeName = typeName;

            AssetDatabase.CreateAsset(def, path);
            created++;
            Debug.Log($"[SkillDefinitionGenerator] 생성 완료: {path} (skillName: {skillName})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Skill Definition Generator",
            created > 0 ? $"{created}개 생성 완료!\nAssets/Skills/ 폴더를 확인하세요." : "새로 생성할 SkillDefinition이 없습니다.",
            "확인"
        );
    }

    private static Type[] GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // 일부 어셈블리는 로드 불가능한 타입을 포함할 수 있음 — 로드 가능한 것만 사용
            return e.Types.Where(t => t != null).ToArray();
        }
    }

    /// <summary>
    /// DefaultActionName()은 protected virtual 메서드라 인스턴스 없이는 호출할 수 없다.
    /// 씬에 보이지 않는 임시 GameObject에 컴포넌트를 붙여 리플렉션으로 호출한 뒤 즉시 파괴한다.
    /// [RequireComponent] 의존성이 있는 액션도 AddComponent가 자동으로 해결해주므로 안전하다.
    /// </summary>
    private static string GetDefaultActionName(Type actionType)
    {
        GameObject temp = null;
        try
        {
            temp = new GameObject("__SkillDefGenTemp") { hideFlags = HideFlags.HideAndDontSave };
            Component comp = temp.AddComponent(actionType);
            MethodInfo method = actionType.GetMethod("DefaultActionName", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return null;
            return method.Invoke(comp, null) as string;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SkillDefinitionGenerator] {actionType.Name}의 기본 이름을 가져오지 못했습니다: {e.Message}");
            return null;
        }
        finally
        {
            if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
        }
    }
}
