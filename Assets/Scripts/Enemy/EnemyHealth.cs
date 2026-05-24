using UnityEngine;
using UnityEngine.AI;

// 적의 체력과 사망 처리를 관리
public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float destroyDelay = 2f;

    public int MaxHealth => stats != null ? stats.MaxHealth : maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = MaxHealth;
    }

    // 외부에서 적에게 피해를 줄 때 호출
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);

        if (CurrentHealth == 0)
        {
            Die();
        }
    }

    // 사망 시 이동과 충돌을 끄고 오브젝트 제거
    private void Die()
    {
        IsDead = true;

        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = false;
        }

        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>())
        {
            enemyCollider.enabled = false;
        }

        Destroy(gameObject, destroyDelay);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }
}
