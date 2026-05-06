using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class StartScreenController : MonoBehaviour
{
    [Header("音声設定")]
    public AudioClip narrationClip;

    public AudioSource audioSource;
    private bool hasFirstEnterPressed = false;
    private bool isNarrationPlaying = false;
    private bool canEnterToLoadScene = false;
    private bool isSceneLoading = false;
    
    void Start()
    {
        // Inspectorで未割り当ての場合のみ同一オブジェクトのAudioSourceを取得する
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    void Update()
    {
        if (!(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            return;
        }

        // 1回目のEnterでナレーションを開始
        if (!hasFirstEnterPressed)
        {
            hasFirstEnterPressed = true;
            StartCoroutine(PlayNarration());
            return;
        }

        // ナレーション再生中のEnterは即座にゲーム画面へ遷移
        if (isNarrationPlaying)
        {
            LoadGameScene();
            return;
        }

        // ナレーション終了後は2回目のEnterで遷移
        if (canEnterToLoadScene)
        {
            LoadGameScene();
        }
    }

    System.Collections.IEnumerator PlayNarration()
    {
        isNarrationPlaying = true;
        canEnterToLoadScene = false;

        if (narrationClip == null)
        {
            Debug.LogWarning("StartScreenController: narrationClip が未設定です。音声なしで待機状態に入ります。");
        }

        if (audioSource != null && narrationClip != null)
        {
            audioSource.clip = narrationClip;
            audioSource.loop = false;
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                yield return null;
            }
        }

        isNarrationPlaying = false;
        canEnterToLoadScene = true;
    }

    void LoadGameScene()
    {
        if (isSceneLoading) return;
        isSceneLoading = true;

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        SceneManager.LoadScene("2DScene");
    }
}
