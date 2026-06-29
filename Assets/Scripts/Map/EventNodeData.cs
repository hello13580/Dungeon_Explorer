using System;
using UnityEngine;

/// <summary>
/// 이벤트 노드의 내용을 정의하는 ScriptableObject.
/// MapNodeData.eventData에 연결해서 사용한다.
/// </summary>
[CreateAssetMenu(fileName = "EventNodeData", menuName = "TBRPG/Map/EventNodeData")]
public class EventNodeData : ScriptableObject
{
    [Tooltip("이벤트 일러스트 이미지 (선택)")]
    public Sprite eventImage;

    [Tooltip("이벤트 제목 (UI 상단에 표시)")]
    public string eventTitle;

    [TextArea(3, 6)]
    [Tooltip("이벤트 배경 설명 텍스트")]
    public string eventDescription;

    [Tooltip("플레이어가 선택할 수 있는 선택지 목록")]
    public EventChoice[] choices;
}

[Serializable]
public class EventChoice
{
    [Tooltip("선택지 버튼에 표시할 텍스트")]
    public string choiceText;

    [Tooltip("이 선택지를 골랐을 때 표시할 결과 설명")]
    [TextArea(2, 4)]
    public string resultText;

    [Tooltip("이 선택지를 골랐을 때 적용할 효과 목록")]
    public EventOutcome[] outcomes;
}

[Serializable]
public class EventOutcome
{
    public EventOutcomeType outcomeType;

    [Tooltip("효과 수치 (골드량, HP량 등)")]
    public int value;
}
