using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class SwordCombatFeedback : MonoBehaviour
{
    [Header("Hit Stop")]
    [SerializeField] private float hitStopDuration = 0.06f;
    [SerializeField] private float hitStopTimeScale = 0.05f;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float shakeForce = 0.7f;

    [Header("Hit VFX")]
    [SerializeField] private ParticleSystem hitVfx;
    [SerializeField] private float hitVfxInterval = 0.05f;

    private float nextHitVfxTime;

    private PlayerDamageDealer damageDealer;
    private Coroutine hitStopRoutine;

    private float originalTimeScale = 1f;

    private void Awake()
    {
        damageDealer = GetComponent<PlayerDamageDealer>();

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    private void OnEnable()
    {
        if (damageDealer != null)
        {
            damageDealer.OnDamageDealt += HandleDamageDealt;
        }
    }

    private void OnDisable()
    {
        if (damageDealer != null)
        {
            damageDealer.OnDamageDealt -= HandleDamageDealt;
        }
    }

    private void HandleDamageDealt(
        int damage,
        PlayerDamageType damageType)
    {
        if (damage <= 0 ||
            damageType != PlayerDamageType.BasicAttack)
        {
            return;
        }

        PlayHitStop();

        if (impulseSource != null)
        {
            impulseSource.GenerateImpulseWithForce(shakeForce);
        }

        if (hitVfx != null &&
            Time.unscaledTime >= nextHitVfxTime)
        {
            hitVfx.Play();

            nextHitVfxTime =
                Time.unscaledTime + hitVfxInterval;
        }
    }

    private void PlayHitStop()
    {
        if (hitStopRoutine != null)
        {
            return;
        }

        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        if (Time.timeScale <= 0f)
        {
            hitStopRoutine = null;
            yield break;
        }

        originalTimeScale = Time.timeScale;

        Time.timeScale = hitStopTimeScale;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        if (Mathf.Approximately(
                Time.timeScale,
                hitStopTimeScale))
        {
            Time.timeScale = originalTimeScale;
        }

        hitStopRoutine = null;
    }
}