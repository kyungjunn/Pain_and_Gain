using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCombatFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Player Damage Flash")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.12f, 0.08f, 1f);
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.14f;

    [Header("Combat Camera Shake")]
    [SerializeField, Min(0f)] private float shakeForce = 0.10f;
    [SerializeField, Min(0f)] private float shakeInterval = 0.1f;

    private PlayerHealth playerHealth;
    private PlayerDamageDealer damageDealer;
    private CombatDamageNumbers damageNumbers;
    private GameManager sessionManager;
    private CinemachineImpulseSource impulseSource;
    private RendererFlash[] rendererFlashes;
    private float damageFlashRemaining;
    private float nextShakeTime = float.NegativeInfinity;
    private int sessionId;
    private bool damageFlashActive;
    private bool sessionEnded;

    private sealed class RendererFlash
    {
        public Renderer target;
        public MaterialPropertyBlock original;
        public MaterialPropertyBlock flashed;
    }

    private void Awake()
    {
        CacheReferences();
        CacheRenderers();
        sessionManager = GameManager.Instance;
        sessionId = sessionManager != null ? sessionManager.SessionId : 0;
        EnsureImpulseSource();
        EnsureImpulseListener();
    }

    private void OnEnable()
    {
        if (sessionEnded)
            return;

        CacheReferences();
        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
            playerHealth.OnDamaged += HandlePlayerDamaged;
        }

        if (damageDealer != null)
        {
            damageDealer.OnTargetDamaged -= HandleTargetDamaged;
            damageDealer.OnTargetDamaged += HandleTargetDamaged;
        }

        if (sessionManager != null && sessionId != 0)
        {
            sessionManager.OnCombatSessionEnding -= HandleSessionEnding;
            sessionManager.OnCombatSessionEnding += HandleSessionEnding;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged -= HandlePlayerDamaged;
        if (damageDealer != null)
            damageDealer.OnTargetDamaged -= HandleTargetDamaged;
        if (sessionManager != null)
            sessionManager.OnCombatSessionEnding -= HandleSessionEnding;

        ClearDamageFlash();
    }

    private void Update()
    {
        if (sessionId != 0 && (sessionManager == null || !sessionManager.BelongsToSession(sessionId)))
        {
            sessionEnded = true;
            enabled = false;
            return;
        }

        if (damageFlashRemaining <= 0f)
            return;

        damageFlashRemaining -= Time.unscaledDeltaTime;
        if (damageFlashRemaining <= 0f)
            ClearDamageFlash();
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (damageDealer == null)
            damageDealer = GetComponent<PlayerDamageDealer>();
        if (damageNumbers == null)
            damageNumbers = GetComponent<CombatDamageNumbers>();
    }

    private void CacheRenderers()
    {
        List<Renderer> bodyRenderers = new List<Renderer>();
        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (IsBodyRenderer(renderer))
                bodyRenderers.Add(renderer);
        }

        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (IsBodyRenderer(renderer))
                bodyRenderers.Add(renderer);
        }

        rendererFlashes = new RendererFlash[bodyRenderers.Count];
        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            rendererFlashes[i] = new RendererFlash
            {
                target = bodyRenderers[i],
                original = new MaterialPropertyBlock(),
                flashed = new MaterialPropertyBlock()
            };
        }
    }

    private bool IsBodyRenderer(Renderer renderer)
    {
        for (Transform current = renderer.transform; current != null && current != transform;
             current = current.parent)
        {
            if (current.GetComponent<ParticleSystem>() != null ||
                current.GetComponent<LineRenderer>() != null ||
                current.GetComponent<TrailRenderer>() != null)
                return false;
        }

        return true;
    }

    private void EnsureImpulseSource()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
            impulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);
        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            impulseSource.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseChannel = 1,
                ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                ImpulseDuration = 0.2f,
                DissipationDistance = 100f
            };
        }
    }

    private void EnsureImpulseListener()
    {
        CinemachineCamera camera = GetComponent<CinemachineCamera>();
        if (camera == null)
            camera = GetComponentInChildren<CinemachineCamera>(true);

        Camera outputCamera = Camera.main;
        if (camera != null)
        {
            if (camera.GetComponent<CinemachineImpulseListener>() != null ||
                (outputCamera != null &&
                 outputCamera.GetComponent<CinemachineExternalImpulseListener>() != null))
                return;

            CinemachineImpulseListener listener =
                camera.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            return;
        }

        if (outputCamera == null ||
            outputCamera.GetComponent<CinemachineExternalImpulseListener>() != null ||
            Object.FindFirstObjectByType<CinemachineImpulseListener>() != null)
            return;

        CinemachineExternalImpulseListener externalListener =
            outputCamera.gameObject.AddComponent<CinemachineExternalImpulseListener>();
        externalListener.ChannelMask = 1;
        externalListener.Gain = 1f;
    }

    private void HandlePlayerDamaged(int actualDamage)
    {
        if (actualDamage <= 0)
            return;

        PlayCameraShake();
        PlayDamageFlash();
        damageNumbers?.ShowDamage(actualDamage, transform.position + Vector3.up * 1.25f, true);
        AudioManager.Instance?.PlayPlayerHit();
        GameManager.Instance?.RequestHitStop();
    }

    private void HandleTargetDamaged(EnemyHealth enemy, Vector3 hitPosition, int actualDamage,
        PlayerDamageType damageType)
    {
        if (enemy == null || actualDamage <= 0)
            return;

        PlayCameraShake();
        damageNumbers?.ShowDamage(actualDamage, hitPosition + Vector3.up * 1.1f, false);
        GameManager.Instance?.RequestHitStop();
    }

    private void HandleSessionEnding(int endingSessionId)
    {
        if (endingSessionId != sessionId)
            return;

        sessionEnded = true;
        enabled = false;
    }

    private void PlayCameraShake()
    {
        if (impulseSource == null || Time.unscaledTime < nextShakeTime)
            return;

        nextShakeTime = Time.unscaledTime + shakeInterval;
        impulseSource.GenerateImpulseWithForce(shakeForce);
    }

    private void PlayDamageFlash()
    {
        if (rendererFlashes == null || rendererFlashes.Length == 0)
            return;

        if (!damageFlashActive)
        {
            foreach (RendererFlash flash in rendererFlashes)
            {
                if (flash.target == null)
                    continue;

                flash.target.GetPropertyBlock(flash.original);
                flash.target.GetPropertyBlock(flash.flashed);
                flash.flashed.SetColor(BaseColorId, damageFlashColor);
                flash.flashed.SetColor(ColorId, damageFlashColor);
                flash.target.SetPropertyBlock(flash.flashed);
            }
            damageFlashActive = true;
        }

        damageFlashRemaining = damageFlashDuration;
    }

    private void ClearDamageFlash()
    {
        if (damageFlashActive && rendererFlashes != null)
        {
            foreach (RendererFlash flash in rendererFlashes)
            {
                if (flash.target != null)
                    flash.target.SetPropertyBlock(flash.original);
            }
        }

        damageFlashRemaining = 0f;
        damageFlashActive = false;
    }

    private void OnValidate()
    {
        damageFlashDuration = Mathf.Max(0.01f, damageFlashDuration);
        shakeForce = Mathf.Max(0f, shakeForce);
        shakeInterval = Mathf.Max(0f, shakeInterval);
    }
}
