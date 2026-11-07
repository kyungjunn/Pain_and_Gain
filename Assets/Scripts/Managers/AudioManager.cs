using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    public AudioSource bgmSource;

    [Header("SFX")]
    public AudioSource sfxSource;

    [Header("SFX Clips")]
    public AudioClip uiClickClip;
    public AudioClip gameStartClip;
    public AudioClip gameOverClip;
    public AudioClip playerHitClip;
    public AudioClip enemyHitClip;
    public AudioClip enemyDeathClip;
    public AudioClip levelUpClip;
    public AudioClip healClip;
    public AudioClip skillDeniedClip;
    public AudioClip encounterClip;
    public AudioClip pauseClip;
    public AudioClip resumeClip;
    private double nextPlayerHitTime;
    private double nextEnemyHitTime;
    private double nextHealTime;
    private double nextDeniedTime;
    private double nextEncounterTime;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayBGM(AudioClip bgmClip)
    {
        if (bgmSource == null || bgmClip == null)
        {
            return;
        }

        bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource == null)
        {
            return;
        }

        bgmSource.Stop();
    }

    public void PlaySFX(AudioClip sfxClip)
    {
        if (sfxSource == null || sfxClip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(sfxClip);
    }

    public void PlayUIClick()
    {
        PlaySFX(uiClickClip);
    }

    public void PlayGameStart()
    {
        PlaySFX(gameStartClip);
    }

    public void PlayGameOver()
    {
        PlaySFX(gameOverClip);
    }

    public void PlayPlayerHit()
    {
        if (Time.unscaledTimeAsDouble < nextPlayerHitTime) return;
        nextPlayerHitTime = Time.unscaledTimeAsDouble + 0.08;
        PlaySFX(playerHitClip);
    }

    public void PlayEnemyHit()
    {
        if (Time.unscaledTimeAsDouble < nextEnemyHitTime) return;
        nextEnemyHitTime = Time.unscaledTimeAsDouble + 0.06;
        PlaySFX(enemyHitClip);
    }

    public void PlayEnemyDeath()
    {
        if (Time.unscaledTimeAsDouble < nextEnemyHitTime) return;
        nextEnemyHitTime = Time.unscaledTimeAsDouble + 0.06;
        PlaySFX(enemyDeathClip);
    }

    public void PlayHeal()
    {
        if (Time.unscaledTimeAsDouble < nextHealTime) return;
        nextHealTime = Time.unscaledTimeAsDouble + 0.2;
        PlaySFX(healClip);
    }

    public void PlaySkillDenied()
    {
        if (Time.unscaledTimeAsDouble < nextDeniedTime) return;
        nextDeniedTime = Time.unscaledTimeAsDouble + 0.2;
        PlaySFX(skillDeniedClip);
    }

    public void PlayEncounter()
    {
        if (Time.unscaledTimeAsDouble < nextEncounterTime) return;
        nextEncounterTime = Time.unscaledTimeAsDouble + 1.0;
        PlaySFX(encounterClip);
    }

    public void PlayPause() => PlaySFX(pauseClip);
    public void PlayResume() => PlaySFX(resumeClip);

    public void PlayLevelUp()
    {
        PlaySFX(levelUpClip);
    }
}
