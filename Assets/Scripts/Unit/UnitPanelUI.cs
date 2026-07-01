using System;
using TMPro;
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

    [Header("수치 텍스트")]
    [SerializeField] private TextMeshProUGUI healthText;      // "현재/최대" 형식
    [SerializeField] private TextMeshProUGUI manaText;        // "현재/최대" 형식
    [SerializeField] private TextMeshProUGUI barrierText;     // 방어막 있을 때만 표시
    [SerializeField] private TextMeshProUGUI unitNameText;    // 유닛 이름
    [SerializeField] private TextMeshProUGUI attackPowerText; // 공격력 수치
    [SerializeField] private TextMeshProUGUI defPowerText;    // 방어력 수치
    [SerializeField] private TextMeshProUGUI manaRegenText;   // 턴당 마나 재생력 (마나 없는 유닛이면 숨김)

    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private BarrierSystem barrierSystem;
    [SerializeField] private ManaSystem manaSystem;
    [SerializeField] private JobPointSystem jobPointSystem;
    private AttackBuffSystem attackBuffSystem;

    [SerializeField] private Unit unit;

    private void Start()
    {
        if (unit != null)
        {
            healthSystem     = unit.GetComponent<HealthSystem>();
            barrierSystem    = unit.GetComponent<BarrierSystem>();
            manaSystem       = unit.GetComponent<ManaSystem>();
            jobPointSystem   = unit.GetComponent<JobPointSystem>();
            attackBuffSystem = unit.GetComponent<AttackBuffSystem>();
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

        if (barrierSystem != null)    barrierSystem.OnBarrierChanged       += BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)       manaSystem.OnManaChanged             += ManaSystem_OnManaChanged;
        if (jobPointSystem != null)   jobPointSystem.OnJobPointsChanged    += JobPointSystem_OnJobPointsChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged        += AttackBuffSystem_OnBuffChanged;

        UpdateActionPointIcons();
        UpdateJobPointIcons();
        UpdateHealthBar();
        UpdateBarrierBar();
        UpdateManaBar();
        UpdateUnitInfo();
    }

    private void UnitActionSystem_OnSelectedUnitChanged(object sender, Unit e)
    {
        if (barrierSystem != null)    barrierSystem.OnBarrierChanged      -= BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)       manaSystem.OnManaChanged            -= ManaSystem_OnManaChanged;
        if (jobPointSystem != null)   jobPointSystem.OnJobPointsChanged   -= JobPointSystem_OnJobPointsChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged       -= AttackBuffSystem_OnBuffChanged;

        unit = UnitActionSystem.Instance.GetSelectedUnit();
        if (unit == null) return;

        healthSystem     = unit.GetComponent<HealthSystem>();
        barrierSystem    = unit.GetComponent<BarrierSystem>();
        manaSystem       = unit.GetComponent<ManaSystem>();
        jobPointSystem   = unit.GetComponent<JobPointSystem>();
        attackBuffSystem = unit.GetComponent<AttackBuffSystem>();

        if (barrierSystem != null)    barrierSystem.OnBarrierChanged      += BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)       manaSystem.OnManaChanged            += ManaSystem_OnManaChanged;
        if (jobPointSystem != null)   jobPointSystem.OnJobPointsChanged   += JobPointSystem_OnJobPointsChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged       += AttackBuffSystem_OnBuffChanged;

        UpdateActionPointIcons();
        UpdateJobPointIcons();
        UpdateHealthBar();
        UpdateBarrierBar();
        UpdateManaBar();
        UpdateUnitInfo();
    }

    private void OnDestroy()
    {
        StageManager.OnStageLoaded -= OnStageLoaded;
        UnitManager.OnStageClear   -= OnStageClear;

        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;

        if (barrierSystem != null)    barrierSystem.OnBarrierChanged      -= BarrierSystem_OnBarrierChanged;
        if (manaSystem != null)       manaSystem.OnManaChanged            -= ManaSystem_OnManaChanged;
        if (jobPointSystem != null)   jobPointSystem.OnJobPointsChanged   -= JobPointSystem_OnJobPointsChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged       -= AttackBuffSystem_OnBuffChanged;
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
        if (healthSystem == null) return;
        if (healthBarImage != null)
            healthBarImage.fillAmount = healthSystem.GetHealthNormalized();
        // "현재/최대" 수치 텍스트 갱신
        if (healthText != null)
            healthText.text = $"{healthSystem.GetCurrentHealth()}/{healthSystem.GetMaxHealth()}";
    }

    private void HealthSystem_OnUnitDamaged(object sender, EventArgs e)
    {
        UpdateHealthBar();
        UpdateBarrierBar();
    }

    private void UpdateBarrierBar()
    {
        bool hasBarrier = barrierSystem != null && barrierSystem.HasBarrier();
        if (barrierBarImage != null)
            barrierBarImage.fillAmount = hasBarrier ? Mathf.Clamp01(barrierSystem.GetBarrierNormalized()) : 0f;
        // 방어막이 있을 때만 수치 표시, 없으면 빈 문자열로 숨김
        if (barrierText != null)
            barrierText.text = hasBarrier ? barrierSystem.GetTotalBarrierAmount().ToString() : "";
    }

    private void BarrierSystem_OnBarrierChanged(object sender, EventArgs e) => UpdateBarrierBar();

    private void UpdateManaBar()
    {
        // 마나 시스템이 없는 유닛은 재생력 텍스트도 숨긴다
        if (manaRegenText != null)
            manaRegenText.text = manaSystem != null ? $"마나 재생 : {manaSystem.GetTotalRegen()}" : "";

        if (manaSystem == null) return;
        if (manaBarImage != null)
            manaBarImage.fillAmount = manaSystem.GetManaNormalized();
        // "현재/최대" 수치 텍스트 갱신
        if (manaText != null)
            manaText.text = $"{manaSystem.GetCurrentMana()}/{manaSystem.GetMaxMana()}";
    }

    private void ManaSystem_OnManaChanged(object sender, EventArgs e) => UpdateManaBar();

    // ── 이름 / 공격력 ────────────────────────────────────────────────

    // 버프가 추가·제거될 때 공격력 수치를 즉시 갱신한다
    private void AttackBuffSystem_OnBuffChanged(object sender, EventArgs e) => UpdateUnitInfo();

    private void UpdateUnitInfo()
    {
        if (unit == null) return;
        if (unitNameText != null)
            unitNameText.text = unit.GetUnitName();
        if (attackPowerText != null)
            attackPowerText.text = $"ATK : {unit.GetAttackPower()}";
        if (defPowerText != null)
            defPowerText.text = $"DEF : {unit.GetDefensePower()}";
    }
}
