using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DetectStandBy : MonoBehaviour
{
    [Header("Vibration Detection")]
    [Tooltip("検知する線形加速度の閾値 (m/s^2) 。ヘッドフォン装着振動は通常小さいので調整して下さい。")]
    public float vibrationThreshold = 1.0f;

    [Tooltip("検知後に再検知するまでのクールダウン（秒）")]
    public float detectionCooldown = 0.5f;

    [Tooltip("デバッグモード")]
    public bool debugMode = true;

    [Header("Events")]
    public UnityEvent onVibrationDetected;

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
