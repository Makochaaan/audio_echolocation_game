using UnityEngine;

/// <summary>
/// チュートリアル専用の猫（目標）コントローラー
/// 位置制御と音声再生に特化したシンプル実装
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class TutorialCatGoalController : MonoBehaviour
{
    [Header("音声設定")]
    public AudioClip meowSound;       // 猫の鳴き声

    private AudioSource audioSource;
    private float gridSize = 1.0f;   // グリッドサイズ

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // Tutorial では確実に聞こえるように 2D 再生にする
        audioSource.playOnAwake = false;

        // 開始位置をグリッドにピッタリ合わせる
        AlignToGrid();
    }

    /// <summary>
    /// 猫をグリッド位置にアライン
    /// </summary>
    void AlignToGrid()
    {
        transform.position = new Vector3(
            Mathf.Round(transform.position.x / gridSize) * gridSize,
            transform.position.y,
            Mathf.Round(transform.position.z / gridSize) * gridSize
        );
    }

    /// <summary>
    /// 猫の位置を指定位置に設定
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        transform.position = new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            position.y,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }

    /// <summary>
    /// 猫の鳴き声を再生
    /// </summary>
    public void PlayMeow()
    {
        Debug.Log($"TutorialCatGoalController.PlayMeow called (clip={(meowSound!=null?meowSound.name:"null")})");
        if (audioSource != null && meowSound != null)
        {
            audioSource.PlayOneShot(meowSound);
        }
    }

    /// <summary>
    /// 猫を有効化（表示）
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        PlayMeow();
    }

    /// <summary>
    /// 猫を無効化（非表示）
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
