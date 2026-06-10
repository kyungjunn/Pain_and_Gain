using UnityEngine;
using TMPro;

// 플레이어 스탯 패널을 현재 생성된 플레이어 데이터와 동기화
public class StatsPanelUI : MonoBehaviour
{
    private GameObject currentPlayerObject;
    private PlayerStats playerStats;
    private PlayerHealth playerHealth;

    public TextMeshProUGUI damageText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI moveSpeedText;
    public TextMeshProUGUI attackSpeedText;
    public TextMeshProUGUI defenseText;

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += InitializeUI;
        // 패널이 나중에 켜져 스폰 이벤트를 놓친 경우를 보정
        TryInitializeFromScenePlayer();
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= InitializeUI;
        UnbindPlayer();
    }

    private void InitializeUI(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        UnbindPlayer();

        currentPlayerObject = playerObject;
        playerStats = playerObject.GetComponent<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.onStatsChanged += Refresh;
        }

        TryBindPlayerHealth();

        Refresh();
    }

    private void TryInitializeFromScenePlayer()
    {
        if (currentPlayerObject != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            InitializeUI(playerObject);
        }
    }

    public void Refresh()
    {
        if (playerStats == null)
        {
            return;
        }

        TryBindPlayerHealth();

        if (damageText != null)
        {
            damageText.text = $"AttackDamage : {playerStats.AttackDamage}";
        }

        if (hpText != null)
        {
            hpText.text = playerHealth != null
                ? $"HP : {playerHealth.CurrentHealth} / {playerHealth.MaxHealth}"
                : $"HP : {playerStats.HP}";
        }

        if (moveSpeedText != null)
        {
            moveSpeedText.text = $"Speed : {playerStats.MoveSpeed}";
        }

        if (attackSpeedText != null)
        {
            attackSpeedText.text = $"AttackSpeed : {playerStats.AttackSpeed}";
        }

        if (defenseText != null)
        {
            defenseText.text = $"Defense : {playerStats.Defense}";
        }
    }

    private void UnbindPlayer()
    {
        if (playerStats != null)
        {
            playerStats.onStatsChanged -= Refresh;
            playerStats = null;
        }

        if (playerHealth != null)
        {
            playerHealth.onHealthChanged -= Refresh;
            playerHealth = null;
        }

        currentPlayerObject = null;
    }

    private void TryBindPlayerHealth()
    {
        if (playerHealth != null || currentPlayerObject == null)
        {
            return;
        }

        playerHealth = currentPlayerObject.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            // 피격/회복 시 HP 텍스트가 즉시 갱신되도록 체력 이벤트 구독
            playerHealth.onHealthChanged += Refresh;
        }
    }
}
