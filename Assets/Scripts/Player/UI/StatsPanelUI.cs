using UnityEngine;
using TMPro;

public class StatsPanelUI : MonoBehaviour
{
    public PlayerStats playerStat;

    public TextMeshProUGUI damageText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI moveSpeedText;
    public TextMeshProUGUI attackSpeedText;
    public TextMeshProUGUI defenseText;

    private void Start()
    {
        playerStat.onStatsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        damageText.text = $"AttackDamage : {playerStat.AttackDamage}";

        hpText.text = $"HP : {playerStat.HP}";

        moveSpeedText.text = $"Speed : {playerStat.MoveSpeed}";

        attackSpeedText.text = $"AttackSpeed : {playerStat.AttackSpeed}";

        defenseText.text = $"Defense : {playerStat.Defense}";
    }
}