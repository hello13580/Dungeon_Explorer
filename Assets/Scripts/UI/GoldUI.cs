using TMPro;
using UnityEngine;

/// <summary>현재 보유 골드를 화면에 표시한다.</summary>
public class GoldUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;

    private void Start()
    {
        GoldSystem.OnGoldChanged += OnGoldChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        GoldSystem.OnGoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(object sender, System.EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (GoldSystem.Instance != null)
            goldText.text = GoldSystem.Instance.GetGold().ToString();
    }
}
