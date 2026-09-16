using System;
using UnityEngine;

public sealed class PlayerUltimateGauge : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxGauge = 100f;
    [SerializeField, Min(0f)] private float currentGauge;
    [SerializeField, Min(0f)] private float gaugePerDamage = 1f;

    private PlayerDamageDealer damageDealer;

    public float Current => currentGauge;
    public float Normalized => currentGauge / maxGauge;
    public bool IsFull => currentGauge >= maxGauge;
    public event Action<float> Changed;

    private void Awake()
    {
        damageDealer = GetComponent<PlayerDamageDealer>();
        if (damageDealer != null)
            damageDealer.OnDamageDealt += HandleDamageDealt;
    }

    private void OnDestroy()
    {
        if (damageDealer != null)
            damageDealer.OnDamageDealt -= HandleDamageDealt;
    }

    public void Add(float amount)
    {
        currentGauge = Mathf.Clamp(currentGauge + Mathf.Max(0f, amount), 0f, maxGauge);
        Changed?.Invoke(Normalized);
    }

    public bool TryConsume()
    {
        if (!IsFull)
            return false;

        currentGauge = 0f;
        Changed?.Invoke(0f);
        return true;
    }

    private void HandleDamageDealt(int damage, PlayerDamageType damageType)
    {
        Add(damage * gaugePerDamage);
    }

    [ContextMenu("Fill Gauge For Test")]
    private void FillForTest()
    {
        currentGauge = maxGauge;
        Changed?.Invoke(1f);
    }
}
