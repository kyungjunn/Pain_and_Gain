using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 체력바. 스포너 또는 부모 플레이어.
public sealed class PlayerHealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private bool bindSpawnedPlayer;
    [SerializeField] private bool faceCamera;

    private PlayerHealth playerHealth;
    private Camera cachedCamera;
    private HealthDamageTrail damageTrail;

    private void Awake()
    {
        if (valueText != null)
        {
            valueText.outlineColor = Color.black;
            valueText.outlineWidth = 0.2f;
            valueText.fontMaterial.EnableKeyword("OUTLINE_ON");
        }
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            CreateDamageTrail();
        }
    }

    private void CreateDamageTrail()
    {
        if (slider.fillRect == null) return;
        damageTrail = HealthDamageTrail.Create(slider.fillRect.parent, Vector2.zero);
    }

    private void OnEnable()
    {
        if (bindSpawnedPlayer)
        {
            // 이벤트 + 현재 스냅샷
            SpawnManager.OnPlayerSpawned += HandlePlayerSpawned;
            HandlePlayerSpawned(SpawnManager.Instance?.SpawnedPlayer);
        }
        else
        {
            BindHealth(GetComponentInParent<PlayerHealth>());
        }
    }

    private void Start()
    {
        // 초기화 순서 차이 대응: 연결 후 체력 재조회
        if (bindSpawnedPlayer)
        {
            HandlePlayerSpawned(SpawnManager.Instance?.SpawnedPlayer);
        }
        else
        {
            BindHealth(GetComponentInParent<PlayerHealth>());
        }

        RefreshHealth();
    }

    private void OnDisable()
    {
        if (bindSpawnedPlayer)
        {
            SpawnManager.OnPlayerSpawned -= HandlePlayerSpawned;
        }

        BindHealth(null);
    }

    private void LateUpdate()
    {
        if (!faceCamera)
        {
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return;
        }

        transform.rotation = cachedCamera.transform.rotation;
    }

    private void HandlePlayerSpawned(GameObject playerObject)
    {
        PlayerHealth health = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        BindHealth(health);
    }

    private void BindHealth(PlayerHealth health)
    {
        if (playerHealth == health)
        {
            RefreshHealth();
            return;
        }

        if (playerHealth != null)
        {
            playerHealth.onHealthChanged -= RefreshHealth;
        }

        playerHealth = health;
        if (damageTrail != null) damageTrail.Clear();

        if (playerHealth != null)
        {
            playerHealth.onHealthChanged += RefreshHealth;
        }

        RefreshHealth();
    }

    private void RefreshHealth()
    {
        if (playerHealth == null)
        {
            if (damageTrail != null) damageTrail.Clear();
            if (slider != null)
            {
                slider.SetValueWithoutNotify(0f);
            }

            if (valueText != null)
            {
                valueText.text = string.Empty;
            }

            return;
        }

        int currentHealth = playerHealth.CurrentHealth;
        int maxHealth = playerHealth.MaxHealth;
        float normalizedHealth = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        if (damageTrail != null) damageTrail.SetHealth(currentHealth, maxHealth);

        if (slider != null)
        {
            slider.SetValueWithoutNotify(normalizedHealth);
        }

        if (valueText != null)
        {
            valueText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}
