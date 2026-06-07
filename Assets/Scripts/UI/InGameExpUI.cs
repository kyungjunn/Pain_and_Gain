using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InGameExpUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expValueText;

    [Header("게이지 속도")]
    [SerializeField] private float fillSpeed = 5f;

    private PlayerLevelSystem playerLevelSystem;
    private float targetProgress = 0f;
    private int displayedLevel = 1;

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += BindLevelSystem;
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= BindLevelSystem;
        UnbindLevelSystem();
    }


    private void BindLevelSystem(GameObject player)
    {
        if (player == null) return;

        playerLevelSystem = player.GetComponent<PlayerLevelSystem>();
        if (playerLevelSystem != null)
        {
            playerLevelSystem.onExpChanged += RefreshExpUI;
            playerLevelSystem.onLevelUp += HandleLevelUp;

            // 초기 데이터 동기화
            displayedLevel = playerLevelSystem.level;
            targetProgress = playerLevelSystem.GetExpPercent();
            expSlider.value = targetProgress;

            UpdateTextUI();
        }
    }

    private void UnbindLevelSystem()
    {
        if (playerLevelSystem != null)
        {
            playerLevelSystem.onExpChanged -= RefreshExpUI;
            playerLevelSystem.onLevelUp -= HandleLevelUp;
        }
    }

    private void Update()
    {
        if (playerLevelSystem == null || expSlider == null) return;

        // Mathf.MoveTowards를 사용해 부드럽게 목표치로 게이지 이동
        expSlider.value = Mathf.MoveTowards(expSlider.value, targetProgress, fillSpeed * Time.deltaTime);

        // 연속 레벨업이나 연출 중 레벨 숫자가 먼저 튀는 걸 방지하기 위해 
        // 게이지가 꽉 차서 초기화되는 타이밍과 디스플레이 레벨을 동기화해 주면 비주얼이 자연스러워짐
    }

    // 경험치 변경 시 호출 (단순 수치 증가)
    private void RefreshExpUI()
    {
        if (playerLevelSystem == null) return;

        targetProgress = playerLevelSystem.GetExpPercent();
        UpdateTextUI();
    }

    // 레벨업 시 호출
    private void HandleLevelUp()
    {
        if (playerLevelSystem == null) return;

        // 레벨업하는 순간 직전 레벨의 바를 꽉 채우는 시각적 연출을 주거나, 즉시 0으로 리셋
        expSlider.value = 0f;
        targetProgress = playerLevelSystem.GetExpPercent();

        displayedLevel = playerLevelSystem.level;
        UpdateTextUI();
    }

    // 텍스트 정보 일괄 갱신
    private void UpdateTextUI()
    {
        if (playerLevelSystem == null) return;

        // 레벨 텍스트 갱신 
        if (levelText != null)
        {
            levelText.text = $"Level : {displayedLevel}";
        }

        // 경험치 디테일 텍스트 갱신
        if (expValueText != null)
        {
            float percent = playerLevelSystem.GetExpPercent() * 100f;
            expValueText.text = $"EXP : {percent:F0}% ({playerLevelSystem.currentExp} / {playerLevelSystem.requiredExp})";
        }
    }
}
