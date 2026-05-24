using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyAttack))]
[RequireComponent(typeof(EnemyHealth))]
// 적의 이동 상태와 추적 행동을 관리하는 AI
public class EnemyAI : MonoBehaviour
{
    // 적 AI 상태
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Dead
    }

    [SerializeField] private EnemyStats stats;
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float repathRate = 0.15f;

    private NavMeshAgent agent;
    private EnemyAttack enemyAttack;
    private EnemyHealth enemyHealth;
    private EnemyState currentState;
    private float nextRepathTime;

    public EnemyState CurrentState => currentState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAttack = GetComponent<EnemyAttack>();
        enemyHealth = GetComponent<EnemyHealth>();

        ApplyStatsToAgent();
    }

    private void Start()
    {
        FindTarget();
    }

    private void Update()
    {
        if (enemyHealth.IsDead)
        {
            SetState(EnemyState.Dead);
            StopAgent();
            return;
        }

        if (target == null)
        {
            FindTarget();
            Idle();
            return;
        }

        float sqrDistance = (target.position - transform.position).sqrMagnitude;
        float attackRange = enemyAttack.AttackRange;

        if (sqrDistance <= attackRange * attackRange)
        {
            Attack();
            return;
        }

        float currentDetectionRange = DetectionRange;

        if (sqrDistance <= currentDetectionRange * currentDetectionRange)
        {
            Chase();
            return;
        }

        Idle();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Player 태그를 가진 오브젝트를 추적 대상으로 설정
    private void FindTarget()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        if (player != null)
        {
            target = player.transform;
        }
    }

    // 대기 상태
    private void Idle()
    {
        SetState(EnemyState.Idle);
        StopAgent();
    }

    // NavMeshAgent를 이용해 플레이어 추적
    private void Chase()
    {
        SetState(EnemyState.Chase);

        if (!CanUseAgent())
        {
            return;
        }

        agent.isStopped = false;

        if (Time.time < nextRepathTime)
        {
            return;
        }

        agent.SetDestination(target.position);
        nextRepathTime = Time.time + RepathRate;
    }

    // 공격 상태
    private void Attack()
    {
        SetState(EnemyState.Attack);
        StopAgent();
        FaceTarget();
        enemyAttack.TryAttack(target);
    }

    // 공격 중 플레이어 방향으로 회전
    private void FaceTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
    }

    // 이동 정지 및 경로 초기화
    private void StopAgent()
    {
        if (!CanUseAgent())
        {
            return;
        }

        agent.isStopped = true;

        if (agent.hasPath)
        {
            agent.ResetPath();
        }
    }

    private bool CanUseAgent()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    // 현재 상태 갱신
    private void SetState(EnemyState nextState)
    {
        currentState = nextState;
    }

    private float DetectionRange => stats != null ? stats.DetectionRange : detectionRange;
    private float RotationSpeed => stats != null ? stats.RotationSpeed : rotationSpeed;
    private float RepathRate => stats != null ? stats.RepathRate : repathRate;

    // EnemyStats 데이터가 있으면 NavMeshAgent에 이동 설정 적용
    private void ApplyStatsToAgent()
    {
        if (stats == null || agent == null)
        {
            return;
        }

        agent.speed = stats.MoveSpeed;
        agent.acceleration = stats.Acceleration;
        agent.stoppingDistance = stats.StoppingDistance;
    }

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0.1f, detectionRange);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        repathRate = Mathf.Max(0.02f, repathRate);

        if (Application.isPlaying)
        {
            ApplyStatsToAgent();
        }
    }
}
