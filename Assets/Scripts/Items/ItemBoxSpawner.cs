using UnityEngine;

public class ItemBoxSpawner : MonoBehaviour
{
    [Header("ItemBox Prefab")]
    [SerializeField] private GameObject boxPrefab;

    [Header("ItemBox Spawn")]
    [SerializeField] private Transform mapCenter;     // 맵의 중심점 오브젝트
    [SerializeField] private float spawnRadius = 15f; // 스폰 반경
    [SerializeField] private int maxBoxCount = 3;    // 배치할 상자 개수

    private void Start()
    {
        SpawnBoxes();
    }

    private void SpawnBoxes()
    {
        if (boxPrefab == null || mapCenter == null) return;

        for (int i = 0; i < maxBoxCount; i++)
        {
            // 중심점 기준 원형 랜덤 좌표 계산
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

            // 바닥 높이(Y축)는 중심점의 높이를 그대로 유지
            Vector3 spawnPosition = new Vector3(
                mapCenter.position.x + randomCircle.x,
                mapCenter.position.y,
                mapCenter.position.z + randomCircle.y
            );

            // 상자 생성
            Instantiate(boxPrefab, spawnPosition, Quaternion.identity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (mapCenter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(mapCenter.position, spawnRadius);
        }
    }
}