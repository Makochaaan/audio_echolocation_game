using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CalibrationController : MonoBehaviour
{
    [Header("Calibration Settings")]
    [Tooltip("キャリブレーション開始までの遅延時間（秒）")]
    public float calibrationDelayTime = 3.0f;

    [Tooltip("WalkingCalibrationInputSystemスクリプト")]
    public WalkingCalibrationInputSystem walkingCalibration;

    [Tooltip("デバッグモード")]
    public bool debugMode = true;

    [Header("Audio Settings")]
    [Tooltip("キャリブレーション完了時の効果音")]
    public AudioClip completionSound;

    [Tooltip("キャリブレーション完了後のナレーション")]
    public AudioClip narrationSound;

    [Tooltip("AudioSource")]
    public AudioSource audioSource;

    private bool calibrationCompleted = false;

    void Start()
    {
        // 起動と同時に音声を再生してキャリブレーション開始処理を実行
        if (audioSource != null && completionSound != null)
        {
            audioSource.Play();
            if (debugMode) Debug.Log("[CalibrationController] Startup sound playing");
        }
        OnSoundFileChanged();
    }

    void Update()
    {
        // キャリブレーション完了を監視
        if (!calibrationCompleted && walkingCalibration != null && walkingCalibration.IsCalibrated)
        {
            calibrationCompleted = true;
            OnCalibrationCompleted();
        }
    }

    /// <summary>
    /// DetectAdjusting から呼ばれる: 音声再生後に遅延してキャリブレーション開始
    /// </summary>
    public void OnSoundFileChanged()
    {
        if (debugMode) Debug.Log($"[CalibrationController] Sound file changed. Starting calibration in {calibrationDelayTime}s");
        Invoke(nameof(StartWalkingCalibration), calibrationDelayTime);
    }

    /// <summary>
    /// WalkingCalibrationInputSystem のキャリブレーション開始メソッドを呼び出す
    /// </summary>
    private void StartWalkingCalibration()
    {
        if (walkingCalibration != null)
        {
            if (debugMode) Debug.Log("[CalibrationController] Starting walking calibration");
            walkingCalibration.StartCalibration();
        }
    }

    /// <summary>
    /// キャリブレーション完了時の処理: 音声再生→シーン遷移
    /// </summary>
    private void OnCalibrationCompleted()
    {
        if (debugMode) Debug.Log("[CalibrationController] Calibration completed!");

        // BGM と注意音声を停止
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // 完了音声を再生
        if (audioSource != null && completionSound != null && narrationSound != null)
        {
            audioSource.PlayOneShot(completionSound);
            if (debugMode) Debug.Log("[CalibrationController] Completion sound playing");

            audioSource.PlayOneShot(narrationSound);
        }

        // 完了音声の再生時間だけ遅延してからシーン遷移
        float delayTime = completionSound != null ? completionSound.length : 1.0f;
        Invoke(nameof(TransitionToNextScene), delayTime);
    }

    /// <summary>
    /// 次のシーンへ遷移
    /// </summary>
    private void TransitionToNextScene()
    {
        if (debugMode) Debug.Log($"[CalibrationController] Transitioning to scene: TutorialScene1");
        SceneManager.LoadScene("TutorialScene1");
    }
}
