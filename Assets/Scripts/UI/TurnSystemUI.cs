using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnSystemUI : MonoBehaviour
{
	[SerializeField]
	private Button endTurnButton;

	[SerializeField]
	private TextMeshProUGUI turnNumberText;

	private void Start()
	{
		UpdateTurnNumberText();
		UpdateEndTurnButtonVisibility();
		endTurnButton.onClick.AddListener(() => TurnSystem.Instance.NextTurn());
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
	}

	public void SetEndTurnButton()
	{
		endTurnButton.onClick.AddListener(TurnSystem.Instance.NextTurn);
	}

	private void UpdateTurnNumberText()
	{
		if (TurnSystem.Instance.IsPlayerTurn())
		{
			turnNumberText.text = "Player Turn " + TurnSystem.Instance.GetTurnNumber();
		}
		else
		{
			turnNumberText.text = "Enemy Turn " + TurnSystem.Instance.GetTurnNumber();
		}
	}

	private void TurnSystem_OnTurnChanged(object sender, EventArgs empty)
	{
		UpdateTurnNumberText();
		UpdateEndTurnButtonVisibility();
	}

	private void UpdateEndTurnButtonVisibility()
	{
		endTurnButton.gameObject.SetActive(TurnSystem.Instance.IsPlayerTurn());
	}
}
