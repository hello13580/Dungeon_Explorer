using System;

/// <summary>
/// 오라 계열 액션이 공통으로 구현하는 인터페이스.
/// AuraVisual이 액션 종류에 관계없이 윤곽선을 그릴 수 있도록 한다.
/// </summary>
public interface IAuraAction
{
    event EventHandler OnAuraActivated;
    event EventHandler OnAuraDeactivated;
    int GetAuraRange();
    Unit GetUnit();
}
