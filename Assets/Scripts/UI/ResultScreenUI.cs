using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 결과 화면 최상위(Ending UI)에 붙이는 스크립트
public class ResultScreenUI : MonoBehaviour
{
    [Header("제목")]
    [SerializeField] private TMP_Text titleText;

    [Header("스탯 텍스트 (왼쪽 열)")]
    [SerializeField] private TMP_Text survivalTimeText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text attackDamageText;

    [Header("스탯 텍스트 (오른쪽 열)")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text attackSpeedText;
    [SerializeField] private TMP_Text defenseText;

    [Header("증강")]
    [SerializeField] private Transform augmentContainer;   // HorizontalLayoutGroup 이 붙은 부모
    [SerializeField] private AugmentSlotUI augmentSlotPrefab;

    [Header("버튼")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("씬 이름")]
    [SerializeField] private string homeSceneName = "IntroScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("에디터 테스트용")]
    [SerializeField] private ResultData testData;

    private void Awake()
    {
        homeButton.onClick.AddListener(OnHome);
        restartButton.onClick.AddListener(OnRestart);
        quitButton.onClick.AddListener(OnQuit);
    }

    private void Start()
    {
        if (GameResult.Data != null)
            Show(GameResult.Data);   // 인게임에서 넘어온 실제 결과
        else if (testData != null)
            Show(testData);          // 엔딩 씬만 단독 실행할 때 테스트용
    }

    // 게임 매니저에서 ResultData 를 채운 뒤 이 함수를 호출
    public void Show(ResultData data)
    {
        // 제목(BATTLE REPORT)은 유니티에서 직접 입력한 모양 그대로 사용하므로 코드에서 건드리지 않음

        int minutes = Mathf.FloorToInt(data.survivalTime / 60f);
        int seconds = Mathf.FloorToInt(data.survivalTime % 60f);
        survivalTimeText.text = $"Survival Time: {minutes:00}:{seconds:00}";

        killsText.text = $"Monster Kills: {data.monsterKills}";
        levelText.text = $"Final Level: {data.finalLevel}";
        attackDamageText.text = $"Attack Damage: {data.attackDamage:0}";

        hpText.text = $"HP: {data.hp:0} / {data.maxHp:0}";
        speedText.text = $"Speed: {data.speed:0.##}";
        attackSpeedText.text = $"Attack Speed: {data.attackSpeed:0.##}";
        defenseText.text = $"Defense: {data.defense:0}";

        BuildAugmentSlots(data);
        gameObject.SetActive(true);
    }

    private void BuildAugmentSlots(ResultData data)
    {
        // 증강 UI를 쓰지 않으면(칸이 비어 있으면) 그냥 건너뜀
        if (augmentContainer == null || augmentSlotPrefab == null) return;

        // 이전 슬롯 정리
        foreach (Transform child in augmentContainer)
            Destroy(child.gameObject);

        foreach (var augment in data.augments)
        {
            var slot = Instantiate(augmentSlotPrefab, augmentContainer);
            slot.Setup(augment);
        }
    }

    private void OnHome() => SceneManager.LoadScene(homeSceneName);
    private void OnRestart() => SceneManager.LoadScene(gameSceneName);

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 인스펙터 우클릭 > "Show Test Data" 로 결과 씬 단독 테스트
    [ContextMenu("Show Test Data")]
    private void ShowTestData()
    {
        if (testData != null) Show(testData);
    }
}
