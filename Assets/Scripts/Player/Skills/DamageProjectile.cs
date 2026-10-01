// Shared flight with either a single-target hit or a one-shot area explosion.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class DamageProjectile : MonoBehaviour
{
    private PlayerDamageDealer damageDealer;
    private PlayerDamageType damageType;
    private Vector3 spawnPosition;
    private float maxDistance;
    private float moveSpeed;
    private int damage;
    private Vector3 moveDirection;
    private Rigidbody body;
    private bool initialized;
    private float explosionRadius;
    private ParticleSystem[] particles;

    // 발사 설정
    public void Initialize(PlayerDamageDealer dealer, PlayerDamageType type, int value,
        float speed, float distance, Vector3 direction, float blastRadius = 0f, float visualScale = 1f)
    {
        damageDealer = dealer;
        damageType = type;
        damage = Mathf.Max(1, value);
        maxDistance = Mathf.Max(0.1f, distance);
        moveSpeed = Mathf.Max(0f, speed);
        moveDirection = direction.normalized;
        spawnPosition = transform.position;
        explosionRadius = Mathf.Max(0f, blastRadius);
        // Scale the artwork, not the flight collider: a large R must not hit early.
        foreach (Transform child in transform)
            child.localScale *= Mathf.Max(0.1f, visualScale);
        if (explosionRadius > 0f)
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                var main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        initialized = true;

        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // 이동 / 사거리 종료
    private void FixedUpdate()
    {
        if (!initialized)
            return;

        float remaining = maxDistance - Vector3.Distance(body.position, spawnPosition);
        if (remaining <= 0.001f)
        {
            Finish(null);
            return;
        }

        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, remaining);
        body.MovePosition(body.position + moveDirection * step);
    }

    // 적 적중
    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || damageDealer == null || other.transform.root == damageDealer.transform)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null)
            return;

        Finish(enemy);
    }

    private void Finish(EnemyHealth directTarget)
    {
        if (!initialized)
            return;

        initialized = false;
        if (explosionRadius <= 0f)
        {
            if (directTarget != null && damageDealer != null)
                damageDealer.DealDamage(directTarget, damage, damageType, spawnPosition);
            Destroy(gameObject);
            return;
        }

        var hitEnemies = new HashSet<EnemyHealth>();
        ApplyExplosionDamage(directTarget, hitEnemies);
        foreach (Collider hit in Physics.OverlapSphere(body.position, explosionRadius, ~0,
                     QueryTriggerInteraction.Collide))
        {
            ApplyExplosionDamage(hit.GetComponentInParent<EnemyHealth>(), hitEnemies);
        }

        foreach (Collider hitbox in GetComponentsInChildren<Collider>())
            hitbox.enabled = false;

        // Reuse the projectile's green particles as a short, stationary burst.
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            var emission = particle.emission;
            emission.enabled = false;
            particle.Play(false);
            particle.Emit(24);
        }
        Destroy(gameObject, 0.6f);
    }

    private void ApplyExplosionDamage(EnemyHealth enemy, HashSet<EnemyHealth> hitEnemies)
    {
        if (damageDealer != null && enemy != null && !enemy.IsDead && hitEnemies.Add(enemy))
            damageDealer.DealDamage(enemy, damage, damageType, spawnPosition);
    }
}
