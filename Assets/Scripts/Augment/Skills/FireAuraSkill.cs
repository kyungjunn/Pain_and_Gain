using System.Collections.Generic;
using UnityEngine;

// 플레이어 주위를 도는 화염 구체. 스택마다 구체가 하나씩 늘고 같은 간격으로 재배치된다.
public class FireAuraSkill : AugmentSkill
{
    [Header("Orbit")]
    // 궤도 반경
    [SerializeField] private float orbitRadius = 2.5f;
    // 초당 회전각
    [SerializeField] private float orbitSpeed = 120f;
    // 구체 크기
    [SerializeField] private float sphereScale = 0.3f;

    [Header("Damage")]
    // 타격 반경
    [SerializeField] private float damageRadius = 0.35f;
    // 타격 주기
    [SerializeField] private float damageInterval = 0.5f;
    // 공격력 계수
    [SerializeField] private float attackDamageRatio = 0.35f;

    private readonly List<Transform> spheres = new List<Transform>();
    private readonly Collider[] hitBuffer = new Collider[32];
    private readonly HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

    private PlayerStats playerStats;
    private PlayerDamageDealer damageDealer;
    private Material sphereMaterial;
    private float orbitAngle;
    private float damageTimer;

    protected override void OnApply()
    {
        playerStats = Player.GetComponent<PlayerStats>();
        damageDealer = Player.GetComponent<PlayerDamageDealer>();
        sphereMaterial = CreateSphereMaterial();
        SyncSphereCount();
    }

    protected override void OnStackChanged()
    {
        SyncSphereCount();
    }

    protected override void OnRemove()
    {
        for (int i = 0; i < spheres.Count; i++)
        {
            if (spheres[i] != null)
            {
                Destroy(spheres[i].gameObject);
            }
        }

        spheres.Clear();

        if (sphereMaterial != null)
        {
            Destroy(sphereMaterial);
            sphereMaterial = null;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        orbitAngle = Mathf.Repeat(orbitAngle + orbitSpeed * Time.deltaTime, 360f);
        UpdateSpherePositions();

        damageTimer -= Time.deltaTime;
        if (damageTimer <= 0f)
        {
            damageTimer = damageInterval;
            DamageNearbyEnemies();
        }
    }

    // 구체 수 동기화
    private void SyncSphereCount()
    {
        while (spheres.Count < StackCount)
        {
            spheres.Add(CreateSphere(spheres.Count));
        }

        while (spheres.Count > StackCount)
        {
            int lastIndex = spheres.Count - 1;
            Transform sphere = spheres[lastIndex];
            spheres.RemoveAt(lastIndex);

            if (sphere != null)
            {
                sphere.gameObject.SetActive(false);
                Destroy(sphere.gameObject);
            }
        }

        UpdateSpherePositions();
    }

    private Transform CreateSphere(int index)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"FireAuraSphere_{index + 1}";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * sphereScale;

        if (sphere.TryGetComponent(out Collider sphereCollider))
        {
            Destroy(sphereCollider);
        }

        if (sphere.TryGetComponent(out Renderer sphereRenderer))
        {
            sphereRenderer.sharedMaterial = sphereMaterial;
        }

        return sphere.transform;
    }

    private Material CreateSphereMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = "FireAura Runtime Material",
            color = Color.red
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.red);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.red);
        }

        return material;
    }

    // 등간격 궤도 배치
    private void UpdateSpherePositions()
    {
        if (spheres.Count == 0)
        {
            return;
        }

        float angleStep = 360f / spheres.Count;
        for (int i = 0; i < spheres.Count; i++)
        {
            if (spheres[i] == null)
            {
                continue;
            }

            float radians = (orbitAngle + angleStep * i) * Mathf.Deg2Rad;
            spheres[i].localPosition = new Vector3(
                Mathf.Cos(radians) * orbitRadius,
                0.8f,
                Mathf.Sin(radians) * orbitRadius);
        }
    }

    // 범위 피해 판정
    private void DamageNearbyEnemies()
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(
            (playerStats != null ? playerStats.AttackDamage : 1f) * attackDamageRatio));

        for (int i = 0; i < spheres.Count; i++)
        {
            Transform sphere = spheres[i];
            if (sphere == null)
            {
                continue;
            }

            damagedEnemies.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(
                sphere.position,
                damageRadius,
                hitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                EnemyHealth enemy = hitBuffer[hitIndex].GetComponentInParent<EnemyHealth>();
                if (enemy != null && damagedEnemies.Add(enemy))
                {
                    if (damageDealer != null)
                    {
                        damageDealer.DealDamage(enemy, damage, PlayerDamageType.Skill);
                    }
                    else
                    {
                        enemy.TakeDamage(damage);
                    }
                }
            }
        }
    }

    private void OnValidate()
    {
        orbitRadius = Mathf.Max(0.1f, orbitRadius);
        sphereScale = Mathf.Max(0.05f, sphereScale);
        damageRadius = Mathf.Max(0.05f, damageRadius);
        damageInterval = Mathf.Max(0.05f, damageInterval);
        attackDamageRatio = Mathf.Max(0f, attackDamageRatio);
    }
}
