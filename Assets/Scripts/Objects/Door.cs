using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MapObject
{
	[SerializeField]
	private List<Door> pairDoorList;

	[SerializeField]
	private bool isOpen;

	[SerializeField]
	private Animator animator;

	protected override void Awake()
	{
		base.Awake();
		if (!pairDoorList.Contains(this))
		{
			pairDoorList.Add(this);
		}
		isOpen = isWalkable;
		animator = GetComponent<Animator>();
	}

	public override string GetObjectName()
	{
		return "Door";
	}

	public override void Interact(Action onInteractComplete)
	{
		StartCoroutine(DoorInteract(onInteractComplete));
	}

	private IEnumerator DoorInteract(Action onInteractComplete)
	{
		bool flag = true;
		foreach (Door pairDoor in pairDoorList)
		{
			if (LevelGrid.Instance.IsGridPositionOccupied(pairDoor.gridPosition))
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			Debug.Log("문 사용");
			foreach (Door pairDoor2 in pairDoorList)
			{
				if (pairDoor2.IsWalkable())
				{
					pairDoor2.GetAnimator().SetBool("IsOpen", false);
					pairDoor2.SetIsWalkable(isWalkable: false);
				}
				else
				{
					pairDoor2.GetAnimator().SetBool("IsOpen", true);
					pairDoor2.SetIsWalkable(isWalkable: true);
				}
				pairDoor2.isOpen = pairDoor2.isWalkable;
			}
		}
		yield return new WaitForSeconds(0.5f);
		onInteractComplete();
	}

	public Animator GetAnimator()
	{
		return animator;
	}
}
