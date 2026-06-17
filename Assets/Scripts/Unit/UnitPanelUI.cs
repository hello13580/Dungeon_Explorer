using System;
using UnityEngine;
using UnityEngine.UI;

public class UnitPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panel; // 패널 루트 오브젝트 — 전투 중에만 표시

    [SerializeField] private Transform actionPointContainer;  // HorizontalLayoutGroup
    [SerializeField] private GameObject actionPointIconPrefab;

    [SerializeField] private Transform jobPointContainer;     // HorizontalLayoutGroup
    [SerializeField] private GameObject jobPointIconPrefab;

    [SerializeField] private Image healthBarImage;
    [SerializeField] private Image barrierBarImage;
    [SerializeField] private Image manaBarImage;

    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private BarrierSystem barrierSystem;
    [SerializeField] private ManaSystem manaSystem;
    [SerializeField] private JobPointSystem jobPointSystem;

    [SerializeField] private Unit unit;

    private void Start()
    {
        if (unit != null)
        {
            healthSystem   = unit.GetComponent<HealthSystem>();
            barrierSystem  = unit.GetComponent<BarrierSystem>();
            manaSystem     = unit.GetComponent<ManaSystem>();
            jobPointSystem = unit.GetComponent<JobPointSystem>();
        }

        // 전투 중에만 패널 표시 — 스테이지 로드 완료 시 표시, 클리어 시 숨김
        StageManager.OnStageLoaded += OnStageLoaded;
        UnitManager.OnStageClear   += OnStageClear;

        // UnitActionSystem은 캐릭터 선택 화면에서 존재하지 않을 수 있으므로 null 체크 후 구독
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnSelectedUnitChanged;

        Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;

        // 시작 시 숨김 — 스테이지 로드 전까지 표시하지 않는다
        if (panel != null) panel.SetActive(false);

        if (healthSystem != null)
            healthSystem.OnUnitDamaged += HealthSystem_OnUnitDamaged;

        if (barrierSystem != null)  barrierSystem.OnBarrierChanged      += BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)     manaSystem.OnManaChanged            += ManaSystem_OnManaChanged;
        if (jobPointSystem != null) jobPointSystem.OnJobPointsChanged   += JobPointSystem_OnJobPointsChanged;

        UpdateActionPointIcons();
        UpdateJobPointIcons();
        UpdateHealthBar();
        UpdateBarrierBar();
        UpdateManaBar();
    }

    private void UnitActionSystem_OnSelectedUnitChanged(object sender, Unit e)
    {
        if (barrierSystem != null) barrierSystem.OnBarrierChanged -= BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)    manaSystem.OnManaChanged       -= ManaSystem_OnManaChanged;
        if (jobPointSystem != null) jobPointSystem.OnJobPointsChanged -= JobPointSystem_OnJobPointsChanged;

        unit = UnitActionSystem.Instance.GetSelectedUnit();
        if (unit == null) return;

        healthSystem   = unit.GetComponent<HealthSystem>();
        barrierSystem  = unit.GetComponent<BarrierSystem>();
        manaSystem     = unit.GetComponent<ManaSystem>();
        jobPointSystem = unit.GetComponent<JobPointSystem>();

        if (barrierSystem != null) barrierSystem.OnBarrierChanged += BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)    manaSystem.OnManaChanged       += ManaSystem_OnManaChanged;
        if (jobPointSystem != null) jobPointSystem.OnJobPointsChanged += JobPointSystem_OnJobPointsChanged;

        UpdateActionPointIcons();
        UpdateJobPointIcons();
        UpdateHealthBar();
        UpdateBarrierBar();
        UpdateManaBar();
    }

    private void OnDestroy()
    {
        StageManager.OnStageLoaded -= OnStageLoaded;
        UnitManager.OnStageClear   -= OnStageClear;

        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;

        if (barrierSystem != null)  barrierSystem.OnBarrierChanged      -= BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)     manaSystem.OnManaChanged            -= ManaSystem_OnManaChanged;
        if (jobPointSystem != null) jobPointSystem.OnJobPointsChanged   -= JobPointSystem_OnJobPointsChanged;
    }

    private void OnStageLoaded(object sender, EventArgs e)
    {
        // 스테이지 로드 완료 → 전투 시작 → 패널 표시
        if (panel != null) panel.SetActive(true);
    }

    private void OnStageClear(object sender, EventArgs e)
    {
        // 스테이지 클리어 → 전투 종료 → 패널 숨김
        if (panel != null) panel.SetActive(false);
    }

    // ── 아이콘 헬퍼 ─────────────────────────────────────────────────

    private void RefreshIcons(Transform container, GameObject prefab, int count)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);

        for (int i = 0; i < count; i++)
            Instantiate(prefab, container);
    }

    // ── 액션 포인트 ─────────────────────────────────────────────────

    private void UpdateActionPointIcons()
    {
        if (unit == null) return;
        RefreshIcons(actionPointContainer, actionPointIconPrefab, unit.GetCurrentActionPoint());
    }

    private void Unit_OnAnyActionPointsChanged(object sender, EventArgs e)
    {
        UpdateActionPointIcons();
    }

    // ── 잡 포인트 ───────────────────────────────────────────────────

    private void UpdateJobPointIcons()
    {
        if (jobPointContainer == null || jobPointIconPrefab == null) return;
        int count = jobPointSystem != null ? jobPointSystem.GetCurrentJobPoints() : 0;
        RefreshIcons(jobPointContainer, jobPointIconPrefab, count);
    }

    private void JobPointSystem_OnJobPointsChanged(object sender, EventArgs e)
    {
        UpdateJobPointIcons();
    }

    // ── HP / 배리어 / 마나 ──────────────────────────────────────────

    private void UpdateHealthBar()
    {
        if (healthBarImage == null || healthSystem == null) return;
        healthBarImage.fillAmount = healthSystem.GetHealthNormalized();
    }

    private void HealthSystem_OnUnitDamaged(object sender, EventArgs e)
    {
        UpdateHealthBar();
        UpdateBarrierBar();
    }

    private void UpdateBarrierBar()
    {
        if (barrierBarImage == null) return;
        barrierBarImage.fillAmount = (barrierSystem != null && barrierSystem.HasBarrier())
            ? Mathf.Clamp01(barrierSystem.GetBarrierNormalized())
            : 0f;
    }

    private void BarrierSystem_OnBarrierChanged(object sender, EventArgs e) => UpdateBarrierBar();

    private void UpdateManaBar()
    {
        if (manaBarImage == null) return;
        manaBarImage.fillAmount = manaSystem != null ? manaSystem.GetManaNormalized() : 0f;
    }

    private void ManaSystem_OnManaChanged(object sender, EventArgs e) => UpdateManaBar();
}
