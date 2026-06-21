using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DetectStandBy : MonoBehaviour
{
    [Header("Vibration Detection")]
    [Tooltip("検知する線形加速度の閾値 (m/s^2) 。ヘッドフォン装着振動は通常小さいので調整して下さい。")]
    public float vibrationThreshold = 0.005f;

    [Tooltip("検知後に再検知するまでのクールダウン（秒）")]
    public float detectionCooldown = 0.5f;

    [Tooltip("デバッグモード")]
    public bool debugMode = true;

    [Tooltip("デバッグログの出力間隔（秒）。0なら毎フレーム出力")]
    public float debugLogInterval = 0.5f;

    [Header("Events")]
    public UnityEvent onVibrationDetected;

    [Header("Auto Transition")]
    [Tooltip("検知処理を無効化し、一定時間後に自動遷移する")]
    public bool autoTransitionOnly = true;

    [Tooltip("自動遷移までの待ち時間（秒）")]
    public float autoTransitionDelay = 10.0f;

    [Header("Orientation Detection")]
    [Tooltip("縦向きに保持された場合に検出を行うか。true のとき加速度検知の代わりに使用します。")]
    public bool useOrientationDetection = true;

    [Tooltip("+Y 方向を向いていると判定する許容角度（度）")]
    public float upwardAngleThreshold = 25.0f;

    [Tooltip("向き判定に使う加速度の平滑化係数（0.01〜1.0）。小さいほど安定")]
    [Range(0.01f, 1.0f)]
    public float orientationSmoothing = 0.1f;

    [Tooltip("向き判定に使う最小加速度の大きさ。これ未満は角度計算しない")]
    public float minOrientationAccel = 0.2f;

    [Tooltip("縦向きが保持される必要がある秒数")]
    public float requiredHoldSeconds = 2.0f;

    // 内部ステート: 縦向きが始まった時刻
    private float orientationStartTime = -10f;

    private float lastDetectTime = -10f;

    private float lastDebugLogTime = -999f;

    private Vector3 smoothedAcc = Vector3.zero;

    private float standbyStartTime = -10f;
    private bool hasAutoTransitioned = false;

    void Start()
    {
        standbyStartTime = Time.time;

        // Accelerometer を有効化（ある場合）
        if (Accelerometer.current != null)
        {
            InputSystem.EnableDevice(Accelerometer.current);
        }

        // LinearAccelerationSensor を有効化（ある場合）
        if (LinearAccelerationSensor.current != null)
        {
            InputSystem.EnableDevice(LinearAccelerationSensor.current);
        }
    }

    void Update()
    {
        if (autoTransitionOnly)
        {
            if (!hasAutoTransitioned && Time.time - standbyStartTime >= autoTransitionDelay)
            {
                hasAutoTransitioned = true;
                if (debugMode) Debug.Log($"[DetectStandBy] Auto transition after {autoTransitionDelay} s.");
                onVibrationDetected?.Invoke();
                SceneManager.LoadScene("2. AdjustingScene");
            }

            return;
        }

        // オリエンテーション検出が有効ならそちらを優先
        if (useOrientationDetection)
        {
            if (Accelerometer.current == null)
                return;

            Vector3 acc = Accelerometer.current.acceleration.ReadValue();
            smoothedAcc = Vector3.Lerp(smoothedAcc, acc, orientationSmoothing);
            float accMag = smoothedAcc.magnitude;
            bool isPortrait = false;

            if (accMag > minOrientationAccel)
            {
                float angleToUp = Vector3.Angle(smoothedAcc / accMag, Vector3.up);
                isPortrait = angleToUp <= upwardAngleThreshold;
            }

            if (debugMode)
            {
                if (debugLogInterval <= 0f || Time.time - lastDebugLogTime >= debugLogInterval)
                {
                    float heldSeconds = orientationStartTime >= 0f ? (Time.time - orientationStartTime) : 0f;
                    float angleToUp = accMag > minOrientationAccel ? Vector3.Angle(smoothedAcc / accMag, Vector3.up) : 999f;
                    Debug.Log($"[DetectStandBy] isUpward={isPortrait}, acc=({acc.x:F3},{acc.y:F3},{acc.z:F3}), smooth=({smoothedAcc.x:F3},{smoothedAcc.y:F3},{smoothedAcc.z:F3}), angleToUp={angleToUp:F1}, held={heldSeconds:F2}");
                    lastDebugLogTime = Time.time;
                }
            }

            if (isPortrait)
            {
                if (orientationStartTime < 0)
                    orientationStartTime = Time.time;

                if (Time.time - orientationStartTime >= requiredHoldSeconds && Time.time - lastDetectTime > detectionCooldown)
                {
                    lastDetectTime = Time.time;
                    orientationStartTime = -10f;
                    if (debugMode) Debug.Log($"[DetectStandBy] Orientation held for {requiredHoldSeconds} s. Triggering.");
                    onVibrationDetected?.Invoke();
                    SceneManager.LoadScene("2. AdjustingScene");
                }
            }
            else
            {
                // 縦向きでなくなったらタイマーをリセット
                orientationStartTime = -10f;
            }

            return;
        }

        // 通常の振動（線形加速度）検出（オプション）
        if (LinearAccelerationSensor.current == null)
            return;

        Vector3 lin = LinearAccelerationSensor.current.acceleration.ReadValue();

        // 線形加速度の大きさを使って振動を検出
        float mag = lin.magnitude;

        if (debugMode)
        {
            if (debugLogInterval <= 0f || Time.time - lastDebugLogTime >= debugLogInterval)
            {
                Debug.Log($"[DetectStandBy] lin=({lin.x:F3},{lin.y:F3},{lin.z:F3}), mag={mag:F3}, threshold={vibrationThreshold:F3}");
                lastDebugLogTime = Time.time;
            }
        }

        if (mag > vibrationThreshold && Time.time - lastDetectTime > detectionCooldown)
        {
            lastDetectTime = Time.time;
            if (debugMode) Debug.Log($"[DetectStandBy] Vibration detected. mag={mag:F3}");
            onVibrationDetected?.Invoke();
            SceneManager.LoadScene("2. AdjustingScene");
        }
    }

    /// <summary>
    /// 外部から振動検知を強制トリガーするユーティリティ
    /// </summary>
    public void TriggerVibration()
    {
        lastDetectTime = Time.time;
        onVibrationDetected?.Invoke();
    }
}
