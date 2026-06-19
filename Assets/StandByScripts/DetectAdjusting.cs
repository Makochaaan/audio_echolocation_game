using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DetectAdjusting : MonoBehaviour
{
    [Header("Stillness Detection")]
    [Tooltip("静止状態と判定する加速度の閾値 (m/s^2)")]
    public float stillnessThreshold = 0.2f;

    [Tooltip("静止状態を判定する時間（秒）")]
    public float stillnessDuration = 5.0f;
    private float narrationWaitDuration = 25.0f;

    [Tooltip("デバッグモード")]
    public bool debugMode = true;

    [Tooltip("デバッグログの出力間隔（秒）。0なら毎フレーム出力")]
    public float debugLogInterval = 0.5f;

    [Header("Events")]
    [Tooltip("5秒間の静止が検知されたときに発火するイベント")]
    public UnityEvent onStillnessDetected;

    [Header("Sound Settings")]
    public AudioClip startupSound;
    [Tooltip("検知完了時の効果音")]
    public AudioClip stillnessSound;
    [Tooltip("検知完了後のBGM")]
    public AudioClip completedBGM;
    [Tooltip("注意事項の音声（SF3）")]
    public AudioClip cautionAudio;

    [Tooltip("BGM再生用のAudioSource")]
    public AudioSource bgAudioSource;
    [Tooltip("注意音声再生用のAudioSource")]
    public AudioSource cautionAudioSource;

    [Tooltip("CalibrationControllerスクリプト")]
    public CalibrationController calibrationController;

    private float stillnessStartTime = -1f;
    private bool isCurrentlyStill = false;
    private float lastDebugLogTime = -999f;

    private bool cautionAudioIsPlayed = false;
    // 一度だけSF3再生→遷移するためのガード
    private bool hasTriggeredSF3 = false;

    void Start()
    {
        // LinearAccelerationSensor を有効化（ある場合）
        if (LinearAccelerationSensor.current != null)
        {
            InputSystem.EnableDevice(LinearAccelerationSensor.current);
        }

        // AudioSource の初期化: bg 用と caution 用を用意
        if (bgAudioSource != null)
        {
            bgAudioSource.clip = startupSound;
            bgAudioSource.loop = true;
            bgAudioSource.Play();
        }

        if (cautionAudioSource == null)
        {
            GameObject go = new GameObject("CautionAudioSource");
            go.transform.SetParent(transform);
            cautionAudioSource = go.AddComponent<AudioSource>();
            cautionAudioSource.loop = true;
        }
    }

    void Update()
    {
        if (cautionAudioIsPlayed) DetectCautionAudioEnd();
        if (LinearAccelerationSensor.current == null) return;
        if (narrationWaitDuration > 0f) narrationWaitDuration -= Time.deltaTime;

        // if (cautionAudioSource.isPlaying && !cautionAudioIsPlayed) return;

        Vector3 lin = LinearAccelerationSensor.current.acceleration.ReadValue();
        float mag = lin.magnitude;

        if (debugMode)
        {
            if (debugLogInterval <= 0f || Time.time - lastDebugLogTime >= debugLogInterval)
            {
                Debug.Log($"[DetectAdjusting] mag={mag:F3}, still={(isCurrentlyStill ? "true" : "false")}, elapsed={(isCurrentlyStill ? (Time.time - stillnessStartTime).ToString("F2") : "0.00")}");
                lastDebugLogTime = Time.time;
            }
        }

        // 加速度が閾値以下 = 静止状態
        if (mag < stillnessThreshold && narrationWaitDuration <= 0f)
        {
            // 静止状態が開始していなければ記録
            if (!isCurrentlyStill)
            {
                stillnessStartTime = Time.time;
                isCurrentlyStill = true;
                if (debugMode) Debug.Log($"[DetectAdjusting] Stillness started. mag={mag:F3}");
            }

            // 静止状態の継続時間をチェック
            float elapsedTime = Time.time - stillnessStartTime;
                if (elapsedTime >= stillnessDuration)
                {
                    if (debugMode) Debug.Log($"[DetectAdjusting] Stillness detected for {stillnessDuration} seconds!");
                    // 既に一度処理済みなら何もしない
                    if (!hasTriggeredSF3)
                    {
                        onStillnessDetected?.Invoke();
                        // 呼び出し側で changeSoundFile を呼ぶ想定。二重実行防止のためフラグを立てる
                        hasTriggeredSF3 = true;
                    }

                    // 再検知を防ぐため、状態をリセット
                    isCurrentlyStill = false;
                    stillnessStartTime = -1f;
                }
        }
        else
        {
            // 加速度が閾値を超えた = 静止状態終了
            if (isCurrentlyStill)
            {
                float elapsedTime = Time.time - stillnessStartTime;
                if (debugMode) Debug.Log($"[DetectAdjusting] Stillness interrupted after {elapsedTime:F2}s. mag={mag:F3}");
                isCurrentlyStill = false;
                stillnessStartTime = -1f;
            }
        }
    }

    /// <summary>
    /// 現在の静止状態をリセット（外部から明示的にリセットしたい場合）
    /// </summary>
    public void ResetStillnessDetection()
    {
        isCurrentlyStill = false;
        stillnessStartTime = -1f;
        lastDebugLogTime = -999f;
        if (debugMode) Debug.Log("[DetectAdjusting] Stillness detection reset.");
    }

    /// <summary>
    /// 現在の経過時間を取得（デバッグ用）
    /// </summary>
    public float GetElapsedStillnessTime()
    {
        if (!isCurrentlyStill) return 0f;
        return Time.time - stillnessStartTime;
    }

    // SF2を止めSF3を開始するためのユーティリティ
    public void changeSoundFile()
    {
        // 停止
        if (bgAudioSource != null) bgAudioSource.Stop();

        // 短い効果音は元の audioSource で再生
        if (stillnessSound != null)
        {
            bgAudioSource.PlayOneShot(stillnessSound);
        }

        // BGM をループ再生（ボリュームは控えめ）
        if (bgAudioSource != null && completedBGM != null)
        {
            bgAudioSource.clip = completedBGM;
            bgAudioSource.loop = true;
            bgAudioSource.volume = 0.6f;
            bgAudioSource.Play();
        }

        // 注意音声をループ再生（優先度高め）。再生時にBGMをダッキング
        if (cautionAudioSource != null && cautionAudio != null)
        {
            // BGMを少し下げる
            if (bgAudioSource != null)
            {
                bgAudioSource.volume = 0.25f;
            }

            cautionAudioSource.clip = cautionAudio;
            cautionAudioSource.loop = false;
            cautionAudioSource.volume = 1.0f;
            cautionAudioSource.Play();
            cautionAudioIsPlayed = true;
        }

        // CalibrationControllerに通知: 遅延後にキャリブレーション開始
        if (calibrationController != null)
        {
            calibrationController.OnSoundFileChanged();
        }
    }

    public void DetectCautionAudioEnd()
    {
        if (cautionAudioSource != null && !cautionAudioSource.isPlaying)
        {
            SceneManager.LoadScene("3. CalibrationScene");
        }
    }
}
