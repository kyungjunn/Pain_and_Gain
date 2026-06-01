using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("BGM")]
    public AudioSource bgmSource;

    private void Start()
    {
        if (bgmSource != null)
        {
            bgmSource.Play();
        }
    }
}