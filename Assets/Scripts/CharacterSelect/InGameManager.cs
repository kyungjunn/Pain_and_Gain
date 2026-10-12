using UnityEngine;

public class IngameManager : MonoBehaviour
{
    [Header("캐릭터 프리팹 목록 (0: 닌자, 1: 기사, 2: 마법사)")]
    public GameObject[] characterPrefabs;

    [Header("캐릭터가 생성될 위치 (Transform)")]
    public Transform spawnPoint;

    void Start()
    {
        // 1. 선택했던 캐릭터 인덱스 불러오기 (기본값: 0번 닌자)
        int selectedIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        Debug.Log("선택된 캐릭터 번호: " + selectedIndex);

        // 2. 인게임 씬 스폰 처리
        SpawnSelectedCharacter(selectedIndex);
    }

    private void SpawnSelectedCharacter(int index)
    {
        if (characterPrefabs != null && characterPrefabs.Length > index)
        {
            if (characterPrefabs[index] != null && spawnPoint != null)
            {
                // 선택한 캐릭터를 스폰 지점에 생성
                Instantiate(characterPrefabs[index], spawnPoint.position, spawnPoint.rotation);
            }
            else
            {
                Debug.LogWarning("InGameManager: characterPrefabs 또는 spawnPoint가 연결되어 있는지 확인해 주세요.");
            }
        }
    }
}