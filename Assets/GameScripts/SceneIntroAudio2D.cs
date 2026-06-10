using System.Collections;
using UnityEngine;

public class SceneIntroAudio2D : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] introClips;

    [Header("Optional")]
    public PlayerController2D playerController;
    public bool disablePlayerInputDuringIntro = true;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        if (disablePlayerInputDuringIntro && playerController != null)
        {
            playerController.enabled = false;
        }

        if (audioSource != null && introClips != null)
        {
            for (int i = 0; i < introClips.Length; i++)
            {
                AudioClip clip = introClips[i];
                if (clip == null) continue;

                audioSource.clip = clip;
                audioSource.Play();

                while (audioSource.isPlaying)
                {
                    yield return null;
                }
            }
        }

        if (disablePlayerInputDuringIntro && playerController != null)
        {
            playerController.enabled = true;
            playerController.ResetInputState();
        }
    }
}
