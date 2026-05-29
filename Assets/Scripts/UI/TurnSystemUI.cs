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
		// 첫 OnTurnChanged 이벤트 전까지 표시하지 않음
		// (Start 시점엔 isPlayerTurn=false, turnNumber=0이라 "Enemy Turn 0"이 잘못 표시되는 문제 방지)
		turnNumberText.text = "";
		endTurnButton.gameObject.SetActive(false);
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
