using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class SkillProjectile : MonoBehaviour
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

    public void Initialize(PlayerDamageDealer dealer, PlayerDamageType type, int value,
        float speed, float distance, Vector3 direction)
    {
        damageDealer = dealer;
        damageType = type;
        damage = Mathf.Max(1, value);
        maxDistance = Mathf.Max(0.1f, distance);
        moveSpeed = Mathf.Max(0f, speed);
        moveDirection = direction.normalized;
        spawnPosition = transform.position;
        initialized = true;

        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        body.MovePosition(body.position + moveDirection * moveSpeed * Time.fixedDeltaTime);

        if ((body.position - spawnPosition).sqrMagnitude >= maxDistance * maxDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || other.transform.root == damageDealer.transform)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null)
            return;

        damageDealer.DealDamage(enemy, damage, damageType);
        Destroy(gameObject);
    }
}
