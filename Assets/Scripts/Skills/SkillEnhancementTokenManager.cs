using System;
using UnityEngine;

/// <summary>
/// 스킬 강화 토큰 보유 개수를 런(run) 동안 보존하는 싱글턴.
/// 스킬 강화 기능 자체는 추후 구현 예정 — 지금은 토큰을 모아두는 저장소 역할만 한다.
/// PartySkillData와 달리 씬에 미리 배치하지 않고, 처음 접근될 때 스스로 생성되어 DontDestroyOnLoad된다.
/// </summary>
public class SkillEnhancementTokenManager : MonoBehaviour
{
    private static SkillEnhancementTokenManager instance;

    public static SkillEnhancementTokenManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject(nameof(SkillEnhancementTokenManager));
                instance = go.AddComponent<SkillEnhancementTokenManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private int tokenCount = 0;

    /// <summary>토큰 개수가 변경될 때 발생. UI 갱신에 사용.</summary>
    public event EventHandler OnTokenCountChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddTokens(int amount)
    {
        tokenCount += amount;
        OnTokenCountChanged?.Invoke(this, EventArgs.Empty);
    }

    public int GetTokenCount() => tokenCount;

    /// <summary>뉴 게임 시 호출해 토큰을 초기화한다.</summary>
    public void ResetAll()
    {
        tokenCount = 0;
        OnTokenCountChanged?.Invoke(this, EventArgs.Empty);
    }
}
