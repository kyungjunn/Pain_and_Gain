using System;
using UnityEngine;


public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    // 플레이어 스폰 시 다른 매니저에 뿌릴 전역 이벤트
    public static event Action<GameObject> OnPlayerSpawned;

    [Header("스폰할 프리팹 설정")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SpawnAll()
    {
        SpawnPlayer();
        SpawnEnemy();
    }

    // 플레이어 스폰
    private void SpawnPlayer()
    {
        // 맵에 배치된 PlayerSpawnPoint인 태그 찾기
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");

        // 스폰 포인트 맵에 존재하는지
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // 스폰 포인트의 랜덤한 인덱스를 선택
            int randomIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
            Transform selectedSpawn = spawnPoints[randomIndex].transform;

            // 선택된 위치와 회전값으로 스폰
            GameObject playerObject = Instantiate(playerPrefab, selectedSpawn.position, selectedSpawn.rotation);

            // 이벤트 전달
            OnPlayerSpawned?.Invoke(playerObject);
        }
        else
        {
            Debug.LogError("PlayerSpawnPoint 태그 찾을 수 없음!");
        }
    }

    // 적 스폰
    private void SpawnEnemy()
    {
        GameObject[] enemyPoints = GameObject.FindGameObjectsWithTag("EnemySpawnPoint");

        if (enemyPoints != null && enemyPoints.Length > 0)
        {
            // 모든 포인트에 좀비 생성
            foreach (GameObject point in enemyPoints)
            {
                Instantiate(enemyPrefab, point.transform.position, point.transform.rotation);
            }
        }
    }
}
