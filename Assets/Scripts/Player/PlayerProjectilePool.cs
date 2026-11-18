using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public enum ProjectileVisualMode
{
    None,
    Basic,
    Empowered
}

// 플레이어별 소유 · 프리팹별 풀 · 연출 모드는 대여별 상태
[DisallowMultipleComponent]
public sealed class PlayerProjectilePool : MonoBehaviour
{
    // 저장 용량만 예약 · 선생성 없음 · 동시 발사 제한 없음
    private const int InitialCapacity = 8;
    private const int MaxRetainedPerPrefab = 32;

    private readonly Dictionary<DamageProjectile, ObjectPool<DamageProjectile>> buckets =
        new Dictionary<DamageProjectile, ObjectPool<DamageProjectile>>();
    private readonly HashSet<DamageProjectile> checkedOut = new HashSet<DamageProjectile>();

    private PlayerDamageDealer dealer;
    private Transform container;
    private Transform staging;
    private GameManager sessionManager;
    private int sessionId;
    private int ownerGeneration;
    private bool intervalOpen;
    private bool managerUnavailableAtOpen;
    private bool manuallyClosed;
    private bool authorityCaptured;
    private bool authorityPermanentlyEnded;
    private bool capturedStandaloneAuthority;
    private GameManager capturedSessionManager;
    private int capturedSessionId;

    private void Awake()
    {
        dealer = GetComponent<PlayerDamageDealer>();
    }

    private void OnEnable()
    {
        if (!manuallyClosed)
            OpenInterval();
    }

    private void OnDisable()
    {
        CloseInterval();
    }

    private void OnDestroy()
    {
        CloseInterval();
    }

    public static PlayerProjectilePool GetOrCreate(PlayerDamageDealer damageDealer)
    {
        if (damageDealer == null)
            return null;

        PlayerProjectilePool pool = damageDealer.GetComponent<PlayerProjectilePool>();
        if (pool == null)
            pool = damageDealer.gameObject.AddComponent<PlayerProjectilePool>();

        // 에디터 직접 호출 보완 · Awake/OnEnable 미호출 대응
        pool.dealer = damageDealer;
        if (pool.isActiveAndEnabled && !pool.manuallyClosed)
            pool.OpenInterval();
        return pool;
    }

    public DamageProjectile Spawn(DamageProjectile prefab, Vector3 position, Quaternion rotation,
        PlayerDamageType type, int damage, float speed, float maxDistance, Vector3 direction,
        float blastRadius = 0f, float visualScale = 1f, int additionalPierces = 0,
        ProjectileVisualMode visualMode = ProjectileVisualMode.None)
    {
        if (prefab == null || !HasAuthority())
            return null;

        ObjectPool<DamageProjectile> bucket = GetBucket(prefab);
        DamageProjectile projectile = bucket.Get();
        if (projectile == null)
            return null;

        // 사용 등록 → 비활성 초기화 → 대여 유효성 재검사
        checkedOut.Add(projectile);
        int capturedOwnerGeneration = ownerGeneration;
        try
        {
            if (!projectile.PrepareLease(this, dealer, capturedOwnerGeneration, position, rotation,
                    type, damage, speed, maxDistance, direction, blastRadius, visualScale,
                    additionalPierces, visualMode))
            {
                checkedOut.Remove(projectile);
                bucket.Release(projectile);
                return null;
            }

            if (!IsLeaseCurrent(projectile, projectile.LeaseId, capturedOwnerGeneration))
                return null;

            return projectile;
        }
        catch
        {
            if (checkedOut.Contains(projectile))
                projectile.CancelFromOwner();
            throw;
        }
    }

    // 명시적 종료 · 재개 금지 · 활성/보관 객체 정리
    public void Close()
    {
        manuallyClosed = true;
        CloseInterval();
    }

    internal bool IsLeaseCurrent(DamageProjectile projectile, int leaseId, int capturedOwnerGeneration)
    {
        if (projectile == null || !intervalOpen || capturedOwnerGeneration != ownerGeneration ||
            !checkedOut.Contains(projectile) || !projectile.IsLeaseCurrent(leaseId, capturedOwnerGeneration))
            return false;

        if (!HasAuthority())
            return false;

        return true;
    }

    internal bool ReturnLease(DamageProjectile projectile, int leaseId, int capturedOwnerGeneration)
    {
        // 이전 대여·이전 세션·중복 반환 차단
        if (projectile == null || !intervalOpen || capturedOwnerGeneration != ownerGeneration ||
            !checkedOut.Contains(projectile) || !projectile.IsLeaseCurrent(leaseId, capturedOwnerGeneration) ||
            !HasAuthority())
            return false;

        DamageProjectile prefab = projectile.PoolPrefab;
        if (prefab == null || !buckets.TryGetValue(prefab, out ObjectPool<DamageProjectile> bucket))
        {
            checkedOut.Remove(projectile);
            projectile.DisposeFromOwner();
            return false;
        }

        checkedOut.Remove(projectile);
        // 상태 복원 후 비활성 보관 · 상한 초과 시 파괴
        projectile.ResetForPool();
        bucket.Release(projectile);
        return true;
    }

    internal void ForgetDisposed(DamageProjectile projectile)
    {
        if (!ReferenceEquals(projectile, null))
            checkedOut.Remove(projectile);
        DisposeContainerIfClosed();
    }

    private ObjectPool<DamageProjectile> GetBucket(DamageProjectile prefab)
    {
        if (buckets.TryGetValue(prefab, out ObjectPool<DamageProjectile> bucket))
            return bucket;

        EnsureContainer();
        bucket = new ObjectPool<DamageProjectile>(
            () => CreateProjectile(prefab),
            actionOnGet: null,
            actionOnRelease: projectile => StoreInactive(projectile),
            actionOnDestroy: projectile => DestroyProjectile(projectile),
            collectionCheck: true,
            defaultCapacity: InitialCapacity,
            maxSize: MaxRetainedPerPrefab);
        buckets.Add(prefab, bucket);
        return bucket;
    }

    private DamageProjectile CreateProjectile(DamageProjectile prefab)
    {
        // 비활성 부모 아래 생성 · 초기화 전 자동 실행 방지
        DamageProjectile projectile = Instantiate(prefab, staging, false);
        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(container, false);
        projectile.InitializePoolObject(prefab);
        return projectile;
    }

    private void EnsureContainer()
    {
        if (container != null)
            return;

        GameObject root = new GameObject($"{name}_ProjectilePool");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        Scene ownerScene = gameObject.scene;
        if (ownerScene.IsValid() && ownerScene.isLoaded)
            SceneManager.MoveGameObjectToScene(root, ownerScene);

        GameObject inactiveStaging = new GameObject("InactiveProjectileStaging");
        inactiveStaging.transform.SetParent(root.transform, false);
        inactiveStaging.SetActive(false);

        container = root.transform;
        staging = inactiveStaging.transform;
    }

    private void StoreInactive(DamageProjectile projectile)
    {
        if (projectile == null)
            return;

        projectile.transform.SetParent(container, false);
        projectile.gameObject.SetActive(false);
    }

    private void DestroyProjectile(DamageProjectile projectile)
    {
        if (projectile == null)
            return;

        projectile.MarkDisposedByPool();
        DestroyForTeardown(projectile.gameObject);
    }

    private bool HasAuthority()
    {
        // 최초 소유 세션만 허용 · 새 세션으로 재바인딩 금지
        if (!intervalOpen || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return false;

        if (managerUnavailableAtOpen || authorityPermanentlyEnded)
            return false;

        if (capturedStandaloneAuthority)
        {
            if (GameManager.Instance == null)
                return true;

            authorityPermanentlyEnded = true;
            CloseInterval();
            return false;
        }

        if (capturedSessionManager != null)
        {
            if (ReferenceEquals(GameManager.Instance, capturedSessionManager) &&
                capturedSessionManager.BelongsToSession(capturedSessionId))
                return true;

            authorityPermanentlyEnded = true;
            CloseInterval();
            return false;
        }

        return false;
    }

    private void OpenInterval()
    {
        dealer = dealer != null ? dealer : GetComponent<PlayerDamageDealer>();
        if (manuallyClosed || intervalOpen || !isActiveAndEnabled || !gameObject.activeInHierarchy ||
            authorityPermanentlyEnded)
            return;

        ownerGeneration++;
        intervalOpen = false;
        managerUnavailableAtOpen = false;
        sessionManager = null;
        sessionId = 0;

        // 최초 권한 고정 · 매니저 없는 테스트 씬은 독립 실행
        if (!authorityCaptured)
        {
            GameManager currentManager = GameManager.Instance;
            authorityCaptured = true;
            if (currentManager == null)
            {
                capturedStandaloneAuthority = true;
            }
            else
            {
                capturedSessionManager = currentManager;
                capturedSessionId = currentManager.SessionId;
                if (!currentManager.IsActiveSession || capturedSessionId == 0)
                    managerUnavailableAtOpen = true;
            }
        }

        if (managerUnavailableAtOpen || (capturedStandaloneAuthority && GameManager.Instance != null))
        {
            authorityPermanentlyEnded = capturedStandaloneAuthority && GameManager.Instance != null;
            return;
        }

        if (capturedStandaloneAuthority)
        {
            intervalOpen = true;
            return;
        }

        if (capturedSessionManager == null ||
            !ReferenceEquals(GameManager.Instance, capturedSessionManager) ||
            !capturedSessionManager.BelongsToSession(capturedSessionId))
        {
            authorityPermanentlyEnded = true;
            return;
        }

        sessionManager = capturedSessionManager;
        sessionId = capturedSessionId;
        sessionManager.OnCombatSessionEnding -= HandleCombatSessionEnding;
        sessionManager.OnCombatSessionEnding += HandleCombatSessionEnding;
        intervalOpen = true;
    }

    private void HandleCombatSessionEnding(int endingSessionId)
    {
        if (endingSessionId == sessionId)
        {
            authorityPermanentlyEnded = true;
            CloseInterval();
        }
    }

    private void CloseInterval()
    {
        // 신규 발사 차단 → 세대 무효화 → 사용 중 취소 → 보관 풀 비우기
        bool hasResources = intervalOpen || checkedOut.Count > 0 || buckets.Count > 0 || container != null;
        intervalOpen = false;
        ownerGeneration++;

        if (sessionManager != null)
            sessionManager.OnCombatSessionEnding -= HandleCombatSessionEnding;
        sessionManager = null;
        sessionId = 0;

        if (!hasResources)
            return;

        var leased = new List<DamageProjectile>(checkedOut);
        for (int i = 0; i < leased.Count; i++)
        {
            DamageProjectile projectile = leased[i];
            if (projectile != null)
                projectile.CancelFromOwner();
        }

        foreach (ObjectPool<DamageProjectile> bucket in buckets.Values)
            bucket.Clear();
        buckets.Clear();

        DisposeContainerIfClosed();
    }

    private void DisposeContainerIfClosed()
    {
        // 피해 콜백 종료 후 마지막 사용 객체까지 정리된 경우 컨테이너 제거
        if (intervalOpen || buckets.Count != 0 || checkedOut.Count != 0 || container == null)
            return;

        DestroyForTeardown(container.gameObject);
        container = null;
        staging = null;
    }

    private static void DestroyForTeardown(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
