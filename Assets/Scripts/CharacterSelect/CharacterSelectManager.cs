using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("캐릭터 및 연기 이펙트 (0: 닌자, 1: 기사, 2: 마법사)")]
    public GameObject[] smokeEffects;

    [Header("이동할 인게임 씬 이름")]
    public string inGameSceneName = "InGame"; // 실제 인게임 씬 파일 이름으로 설정하세요.

    private static readonly PlayerCharacterId[] ButtonCharacters =
    {
        PlayerCharacterId.Ninja, PlayerCharacterId.Sword, PlayerCharacterId.Wizard
    };

    private PlayerCharacterId selectedCharacter = PlayerCharacterId.Ninja;

    void Start()
    {
        // 시작할 때 기본으로 0번(닌자) 캐릭터만 연기 켜기
        SelectCharacter(0);
    }

    void Update()
    {
        // 게임 내 다른 스크립트가 커서를 감추더라도 매 프레임 마우스 커서를 강제로 표출
        if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // 캐릭터 버튼(발판/캐릭터) 클릭 시 호출할 함수
    public void SelectCharacter(int index)
    {
        if (index < 0 || index >= ButtonCharacters.Length)
        {
            Debug.LogError($"Invalid character button index: {index}", this);
            return;
        }

        selectedCharacter = ButtonCharacters[index];

        // 선택한 캐릭터 연기만 활성화, 나머지 비활성화
        for (int i = 0; i < smokeEffects.Length; i++)
        {
            if (smokeEffects[i] != null)
            {
                smokeEffects[i].SetActive(i == index);
            }
        }

    }

    // START 버튼 누를 때 호출할 함수
    public void OnClickStartButton()
    {
        CharacterSelection.Confirm(selectedCharacter);
        // 인게임 씬으로 이동
        SceneManager.LoadScene(inGameSceneName);
    }
}