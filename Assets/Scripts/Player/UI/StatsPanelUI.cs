using UnityEngine;
using TMPro;

public class StatsPanelUI : MonoBehaviour
{
    private PlayerStats playerStats;

    public TextMeshProUGUI damageText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI moveSpeedText;
    public TextMeshProUGUI attackSpeedText;
    public TextMeshProUGUI defenseText;

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += InitializeUI;
    }
    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= InitializeUI;
        if (playerStats != null)
        {
            playerStats.onStatsChanged -= Refresh;
        }
    }

    // 플레이어 스폰 시 자동 실행
    private void InitializeUI(GameObject playerObject)
    {
        playerStats = playerObject.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.onStatsChanged += Refresh;
            Refresh();
        }
    }

    public void Refresh()
    {
        damageText.text = $"AttackDamage : {playerStats.AttackDamage}";

        hpText.text = $"HP : {playerStats.HP}";

        moveSpeedText.text = $"Speed : {playerStats.MoveSpeed}";

        attackSpeedText.text = $"AttackSpeed : {playerStats.AttackSpeed}";

        defenseText.text = $"Defense : {playerStats.Defense}";
    }
}