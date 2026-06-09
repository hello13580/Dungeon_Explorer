using System;
using UnityEngine;

/// <summary>
/// 파티 전체의 골드를 스테이지 간 보존하는 싱글턴.
/// DontDestroyOnLoad로 유지된다.
/// </summary>
public class GoldSystem : MonoBehaviour
{
    public static GoldSystem Instance { get; private set; }

    public static event EventHandler OnGoldChanged;

    private int currentGold = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetGold() => currentGold;

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        currentGold += amount;
        OnGoldChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool SpendGold(int amount)
    {
        if (amount > currentGold) return false;
        currentGold -= amount;
        OnGoldChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>뉴 게임 시 호출해 골드를 초기화한다.</summary>
    public void ResetGold()
    {
        currentGold = 0;
        OnGoldChanged?.Invoke(this, EventArgs.Empty);
    }
}
