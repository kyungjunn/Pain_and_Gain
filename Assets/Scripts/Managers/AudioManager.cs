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
    public AudioClip playerAttackClip;
    public AudioClip playerHitClip;
    public AudioClip enemyHitClip;
    public AudioClip enemyDeathClip;
    public AudioClip levelUpClip;
    private float nextPlayerHitTime;
    private float nextEnemyHitTime;

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

    public void PlayPlayerAttack()
    {
        PlaySFX(playerAttackClip);
    }

    public void PlayPlayerHit()
    {
        if (Time.unscaledTime < nextPlayerHitTime) return;
        nextPlayerHitTime = Time.unscaledTime + 0.08f;
        PlaySFX(playerHitClip);
    }

    public void PlayEnemyHit()
    {
        if (Time.unscaledTime < nextEnemyHitTime) return;
        nextEnemyHitTime = Time.unscaledTime + 0.06f;
        PlaySFX(enemyHitClip);
    }

    public void PlayEnemyDeath()
    {
        PlaySFX(enemyDeathClip);
    }

    public void PlayLevelUp()
    {
        PlaySFX(levelUpClip);
    }
}
