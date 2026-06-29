using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// WaitingScene 用: 端末を所定の向き（z ≈ -1g）で静止保持し続けたら
// ゲーム開始トリガーとして次シーンへ遷移する。
// 検出ロジックは test_android_IMU プロジェクトの DetectStandBy を踏襲。
public class DetectStandBy : MonoBehaviour
{
    [Header("StandBy Detection (IMU)")]
    [Tooltip("ゲーム開始までに静止保持が必要な秒数（デフォルト5秒）")]
    [SerializeField] private float standByThres = 5.0f;

    [Tooltip("正規化した重力方向 z の上限（-1g 付近を上向き判定。0に近いほど許容が緩い）")]
    [SerializeField] private float zMaxThres = -0.9f;

    [Tooltip("正規化した重力方向 z の下限（正規化後は最小 -1 なので実質は安全マージン）")]
    [SerializeField] private float zMinThres = -1.1f;

    [Tooltip("静止とみなす線形加速度の大きさの上限")]
    [SerializeField] private float magThres = 0.1f;

    [Header("Transition")]
    [Tooltip("静止保持が完了したら遷移するシーン名")]
    [SerializeField] private string nextSceneName = "2. AdjustingScene";

    [Tooltip("遷移直前に追加で呼びたい処理があれば登録")]
    public UnityEvent onStandByComplete;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    [Tooltip("デバッグログの出力間隔（秒）。0なら毎フレーム")]
    [SerializeField] private float debugLogInterval = 0.5f;

    private Vector3 accel = Vector3.zero;
    private Vector3 linearAccel = Vector3.zero;
    private float standByDuration = 0.0f;
    private float lastDebugLogTime = -999f;
    private bool hasTriggered = false;

    void Start()
    {
        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);

        if (UnityEngine.InputSystem.Gyroscope.current != null)
            InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);

        if (AttitudeSensor.current != null)
            InputSystem.EnableDevice(AttitudeSensor.current);

        if (LinearAccelerationSensor.current != null)
        {
            InputSystem.EnableDevice(LinearAccelerationSensor.current);
            if (debugMode) Debug.Log("[DetectStandBy] LinearAccelerationSensor available");
        }
        else if (debugMode)
        {
            Debug.Log("[DetectStandBy] LinearAccelerationSensor not available");
        }
    }

    void Update()
    {
        if (hasTriggered) return;

        if (Accelerometer.current != null)
            accel = Accelerometer.current.acceleration.ReadValue();

        if (LinearAccelerationSensor.current != null)
            linearAccel = LinearAccelerationSensor.current.acceleration.ReadValue();

        // 重力加速度を正規化し、向き（z成分）だけで判定する。
        // こうすると実機ごとの 1g の誤差（例: -1.021）に左右されない。
        float gravityZ = accel.normalized.z;
        bool isStandBy = zMinThres <= gravityZ && gravityZ <= zMaxThres
                         && linearAccel.magnitude <= magThres;

        if (isStandBy)
            standByDuration += Time.deltaTime;
        else
            standByDuration = 0.0f;

        if (debugMode && (debugLogInterval <= 0f || Time.time - lastDebugLogTime >= debugLogInterval))
        {
            Debug.Log($"[DetectStandBy] accel.z={accel.z:F3}, gz={gravityZ:F3}, mag={linearAccel.magnitude:F3}, " +
                      $"standby={isStandBy}, duration={standByDuration:F2}/{standByThres:F1}");
            lastDebugLogTime = Time.time;
        }

        if (standByDuration >= standByThres)
        {
            hasTriggered = true;
            if (debugMode) Debug.Log("[DetectStandBy] StandBy complete -> start game!");
            onStandByComplete?.Invoke();
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
