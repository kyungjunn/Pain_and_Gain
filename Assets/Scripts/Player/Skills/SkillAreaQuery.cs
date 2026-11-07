using System;
using System.Collections.Generic;
using UnityEngine;

// 효과별 범위 조회: 다중 충돌체 중복 제거·밀집 대상 누락 방지
public sealed class SkillAreaQuery
{
    private Collider[] colliders = new Collider[32];
    private readonly HashSet<EnemyHealth> uniqueEnemies = new HashSet<EnemyHealth>();
    private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();

    public IReadOnlyList<EnemyHealth> Collect(Vector3 center, float radius, int layers = ~0)
    {
        int count;
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, radius), colliders,
                layers, QueryTriggerInteraction.Collide);
            if (count < colliders.Length)
                break;
            // 버퍼 포화 시 확장 후 재조회: 밀집 대상 누락 방지 (32, 64, 128 .. )
            Array.Resize(ref colliders, colliders.Length * 2);
        }

        uniqueEnemies.Clear();
        enemies.Clear();
        for (int i = 0; i < count; i++)
        {
            EnemyHealth enemy = colliders[i].GetComponentInParent<EnemyHealth>();
            colliders[i] = null;
            if (enemy != null && !enemy.IsDead && uniqueEnemies.Add(enemy))
                enemies.Add(enemy);
        }
        return enemies;
    }

    public void DealDamage(PlayerDamageDealer dealer, Vector3 center, float radius, int damage,
        PlayerDamageType type, Vector3? source = null, EnemyHealth directTarget = null)
    {
        if (dealer == null)
            return;

        Collect(center, radius);
        if (directTarget != null && !directTarget.IsDead && uniqueEnemies.Add(directTarget))
            enemies.Add(directTarget);

        // 효과별 조회 분리: 피해 콜백의 다른 효과 조회 허용
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                dealer.DealDamage(enemy, damage, type, source ?? center);
        }
    }
}
