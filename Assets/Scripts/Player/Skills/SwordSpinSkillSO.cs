using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Player/Skills/Sword Spin")]
public sealed class SwordSpinSkillSO : PlayerSkillSO
{
    [Header("Spin Attack")]
    [SerializeField, Min(0.1f)] private float radius = 3f;
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem spinVfxPrefab;
    [SerializeField] private Vector3 vfxOffset =
        new Vector3(0f, 0.8f, 0f);

    [Tooltip("버튼 입력 후 실제 데미지가 발생하기까지 시간")]
    [SerializeField, Min(0f)] private float hitDelay = 0.15f;

    [SerializeField] private Vector3 centerOffset =
        new Vector3(0f, 0.8f, 0f);

    [SerializeField] private LayerMask enemyLayers = ~0;

    public override bool Cast(
        PlayerSkillController owner,
        PlayerDamageType damageType)
    {
        if (owner == null ||
            owner.DamageDealer == null ||
            owner.Stats == null)
        {
            return false;
        }

        owner.StartCoroutine(
            ExecuteSpin(owner, damageType)
        );

        return true;
    }

    private IEnumerator ExecuteSpin(
        PlayerSkillController owner,
        PlayerDamageType damageType)
    {
        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);

        if (owner == null ||
            owner.DamageDealer == null ||
            owner.Stats == null)
        {
            yield break;
        }

        if (spinVfxPrefab != null)
        {
            Quaternion vfxRotation =
                owner.transform.rotation * spinVfxPrefab.transform.rotation;

            ParticleSystem vfx = Instantiate(
                spinVfxPrefab,
                owner.transform.position + vfxOffset,
                vfxRotation
            );

            vfx.Play();

            ParticleSystem.MainModule main = vfx.main;

            float destroyDelay =
                main.duration +
                main.startLifetime.constantMax +
                0.2f;

            Destroy(vfx.gameObject, destroyDelay);
        }

        Vector3 center =
            owner.transform.position + centerOffset;

        Collider[] hits = Physics.OverlapSphere(
            center,
            radius,
            enemyLayers,
            QueryTriggerInteraction.Ignore
        );

        HashSet<EnemyHealth> hitEnemies =
            new HashSet<EnemyHealth>();

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(
                owner.Stats.AttackDamage *
                damageMultiplier
            )
        );

        foreach (Collider hit in hits)
        {
            EnemyHealth enemy =
                hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.IsDead ||
                !hitEnemies.Add(enemy))
            {
                continue;
            }

            owner.DamageDealer.DealDamage(
                enemy,
                damage,
                damageType
            );
        }
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        damageMultiplier =
            Mathf.Max(0f, damageMultiplier);
        hitDelay = Mathf.Max(0f, hitDelay);
    }
}