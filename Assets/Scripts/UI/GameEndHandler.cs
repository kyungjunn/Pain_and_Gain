using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 사망 결과 저장 후 세션 종료 요청
public class GameEndHandler : MonoBehaviour
{
    private PlayerStateManager stateManager;
    private PlayerStats stats;
    private PlayerLevelSystem levelSystem;
    private float combatStartTime;

    [Header("설정")]
    [SerializeField] private float delayBeforeEnding = 1.5f; // 죽고 나서 엔딩으로 넘어가기까지 대기(초)
    [SerializeField] private string endingSceneName = "EndingScene";

    private bool ended;

    private void Awake()
    {
        ended = false;
    }

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += BindPlayer;
        // 이벤트 + 현재 스냅샷
        if (SpawnManager.Instance != null)
            BindPlayer(SpawnManager.Instance.SpawnedPlayer);
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= BindPlayer;
        UnbindPlayer();
        StopAllCoroutines();
    }

    private void UnbindPlayer()
    {
        if (stateManager != null) stateManager.OnDead -= HandleDead;
        stateManager = null;
        stats = null;
        levelSystem = null;
    }

    private void BindPlayer(GameObject player)
    {
        if (stateManager != null && stateManager.gameObject == player) return;
        UnbindPlayer();
        if (player == null || ended) return;
        if (!player.TryGetComponent(out stateManager)
            || !player.TryGetComponent(out stats)
            || !player.TryGetComponent(out levelSystem))
        {
            Debug.LogError("GameEndHandler requires player state, stats and level components.", this);
            UnbindPlayer();
            return;
        }

        combatStartTime = Time.time;
        stateManager.OnDead += HandleDead;
    }

    private void HandleDead()
    {
        if (ended) return; // 두 번 실행 방지
        ended = true;

        GameResult.Data = BuildResult(); // 사망 순간 수치
        // 결과 보존 후 세션 종료
        GameManager.Instance?.NotifyPlayerUnavailable(stateManager.gameObject);
        StartCoroutine(LoadEndingAfterDelay());
    }

    private ResultData BuildResult()
    {
        return ResultData.Capture(stats, levelSystem, stats.GetComponent<PlayerAugments>(),
            Time.time - combatStartTime, RunStats.Kills);
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
