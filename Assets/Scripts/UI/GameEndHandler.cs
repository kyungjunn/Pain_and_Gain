using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 인게임 씬의 빈 오브젝트(예: GameEndHandler)에 붙이는 스크립트
public class GameEndHandler : MonoBehaviour
{
    [Header("비워 두면 자동으로 찾음")]
    [SerializeField] private PlayerStateManager stateManager;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerLevelSystem levelSystem;

    [Header("설정")]
    [SerializeField] private float delayBeforeEnding = 1.5f; // 죽고 나서 엔딩으로 넘어가기까지 대기(초)
    [SerializeField] private string endingSceneName = "EndingScene";

    private bool ended;

    private void Awake()
    {
        RunStats.Reset(); // 새 판 시작: 킬 수 초기화
        ended = false;
    }

    private void OnEnable()
    {
        StartCoroutine(FindPlayerAndSubscribe());
    }

    private void OnDisable()
    {
        if (stateManager != null) stateManager.OnDead -= HandleDead;
    }

    // 플레이어가 나중에 생성되는 경우도 있으니, 찾을 때까지 기다렸다가 연결
    private IEnumerator FindPlayerAndSubscribe()
    {
        while (stateManager == null || stats == null || levelSystem == null)
        {
            if (stateManager == null) stateManager = FindFirstObjectByType<PlayerStateManager>();
            if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
            if (levelSystem == null) levelSystem = FindFirstObjectByType<PlayerLevelSystem>();
            yield return new WaitForSecondsRealtime(0.2f);
        }

        stateManager.OnDead -= HandleDead; // 중복 연결 방지
        stateManager.OnDead += HandleDead;
    }

    private void HandleDead()
    {
        if (ended) return; // 두 번 실행 방지
        ended = true;

        GameResult.Data = BuildResult(); // 죽은 순간의 값을 바로 저장
        StartCoroutine(LoadEndingAfterDelay());
    }

    private ResultData BuildResult()
    {
        return new ResultData
        {
            isVictory = false,
            survivalTime = Time.timeSinceLevelLoad, // 씬 시작 후 흐른 시간(일시정지 시간은 제외)
            monsterKills = RunStats.Kills,
            finalLevel = levelSystem.level,

            attackDamage = stats.AttackDamage,
            hp = 0f,                // 죽은 순간이라 현재 체력은 0
            maxHp = stats.HP,       // PlayerStats.HP는 최대 체력
            speed = stats.MoveSpeed,
            attackSpeed = stats.AttackSpeed,
            defense = stats.Defense
        };
    }

    private IEnumerator LoadEndingAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBeforeEnding);
        Time.timeScale = 1f; // 혹시 멈춰 있었다면 풀고 이동

        Cursor.lockState = CursorLockMode.None; // 커서 잠금 해제
        Cursor.visible = true;                  // 커서 보이게

        SceneManager.LoadScene(endingSceneName);
    }
}
