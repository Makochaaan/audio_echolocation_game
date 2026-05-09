using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DetectStandBy : MonoBehaviour
{
    [Header("Vibration Detection")]
    [Tooltip("検知する線形加速度の閾値 (m/s^2) 。ヘッドフォン装着振動は通常小さいので調整して下さい。")]
    public float vibrationThreshold = 0.05f;

    [Tooltip("検知後に再検知するまでのクールダウン（秒）")]
    public float detectionCooldown = 0.5f;

    [Tooltip("デバッグモード")]
    public bool debugMode = true;

    [Header("Events")]
    public UnityEvent onVibrationDetected;

    [Header("Orientation Detection")]
    [Tooltip("縦向きに保持された場合に検出を行うか。true のとき加速度検知の代わりに使用します。")]
    public bool useOrientationDetection = true;

    [Tooltip("縦向きが保持される必要がある秒数")]
    public float requiredHoldSeconds = 2.0f;

    // 内部ステート: 縦向きが始まった時刻
    private float orientationStartTime = -10f;

    private float lastDetectTime = -10f;

    void Start()
    {
        // LinearAccelerationSensor を有効化（ある場合）
        if (LinearAccelerationSensor.current != null)
        {
            InputSystem.EnableDevice(LinearAccelerationSensor.current);
        }
    }

    void Update()
    {
        // オリエンテーション検出が有効ならそちらを優先
        if (useOrientationDetection)
        {
            // 端末の向きを Unity の DeviceOrientation から判定
            var devOrient = Input.deviceOrientation;

            bool isPortrait = (devOrient == DeviceOrientation.Portrait || devOrient == DeviceOrientation.PortraitUpsideDown);

            // デバイスオリエンテーションが Unknown の場合、加速度センサーで縦向きらしさを推定するフォールバック
            if (devOrient == DeviceOrientation.Unknown)
            {
                Vector3 acc = Input.acceleration;
                // 一般的に縦向きでは Y 軸の重力成分が大きくなる
                isPortrait = Mathf.Abs(acc.y) > Mathf.Abs(acc.x) && Mathf.Abs(acc.y) > 0.5f;
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
