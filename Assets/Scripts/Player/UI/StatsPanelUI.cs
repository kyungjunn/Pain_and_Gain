using UnityEngine;
using TMPro;

// 플레이어 스탯 패널을 현재 생성된 플레이어 데이터와 동기화
public class StatsPanelUI : MonoBehaviour
{
    private GameObject currentPlayerObject;
    private PlayerStats playerStats;

    public TextMeshProUGUI damageText;
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
            if (damageText != null)
            {
                damageText.text = string.Empty;
            }

            if (moveSpeedText != null)
            {
                moveSpeedText.text = string.Empty;
            }

            if (attackSpeedText != null)
            {
                attackSpeedText.text = string.Empty;
            }

            if (defenseText != null)
            {
                defenseText.text = string.Empty;
            }

            return;
        }

        if (damageText != null)
        {
            damageText.text = $"공격력: {FormatStat(playerStats.AttackDamage)}";
        }

        if (moveSpeedText != null)
        {
            moveSpeedText.text = $"이동속도: {FormatStat(playerStats.MoveSpeed)}";
        }

        if (attackSpeedText != null)
        {
            attackSpeedText.text = $"공격속도: {FormatStat(playerStats.AttackSpeed)}";
        }

        if (defenseText != null)
        {
            defenseText.text = $"방어력: {FormatStat(playerStats.Defense)}";
        }
    }

    private static string FormatStat(float value)
    {
        return value.ToString("0.##");
    }

    private void UnbindPlayer()
    {
        if (playerStats != null)
        {
            playerStats.onStatsChanged -= Refresh;
            playerStats = null;
        }

        currentPlayerObject = null;
    }
}
