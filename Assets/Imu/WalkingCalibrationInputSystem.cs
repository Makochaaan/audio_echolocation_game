using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WalkingCalibrationInputSystem : MonoBehaviour
{
    // シーン遷移を跨いでキャリブレーション結果を保持する
    public static bool HasPersistedCalibration { get; private set; } = false;
    public static float PersistedThreshold { get; private set; } = 0.0f;
    public static float PersistedMean { get; private set; } = 0.0f;
    public static float PersistedStd { get; private set; } = 0.0f;

    [Header("Calibration Settings")]
    [SerializeField] private float calibrationDuration = 15.0f;
    [SerializeField] private float clipDuration = 1.0f;

    [Header("Smoothing Settings")]
    [SerializeField, Range(0.01f, 1.0f)]
    private float alpha = 0.1f;

    [Header("Threshold Settings")]
    [SerializeField] private float thresholdScale = 0.5f;
    [Header("Threshold Target")]

    [SerializeField] private InterfaceClient interfaceClient;

    [Header("Axis Settings")]
    [SerializeField] private bool useXAxis = false;
    [SerializeField] private bool useYAxis = true;
    [SerializeField] private bool useZAxis = false;

    [Header("Result")]
    public bool IsCalibrated { get; private set; } = false;
    public float Threshold { get; private set; } = 0.0f;
    public float Mean { get; private set; } = 0.0f;
    public float Std { get; private set; } = 0.0f;

    private bool isCalibrating = false;
    private float startTime;

    private readonly List<float> timeList = new List<float>();
    private readonly List<float> rawList = new List<float>();
    private readonly List<float> smoothedList = new List<float>();

    private LinearAccelerationSensor linearAccelerationSensor;

    private void Start()
    {
        linearAccelerationSensor = LinearAccelerationSensor.current;

        if (linearAccelerationSensor == null)
        {
            Debug.LogError("LinearAccelerationSensor is not available on this device.");
            enabled = false;
            return;
        }

        InputSystem.EnableDevice(linearAccelerationSensor);

        // 既にキャリブレーション結果がある場合は復元
        if (HasPersistedCalibration)
        {
            IsCalibrated = true;
            Threshold = PersistedThreshold;
            Mean = PersistedMean;
            Std = PersistedStd;

            if (interfaceClient != null)
            {
                interfaceClient.SetWalkingThreshold(Threshold);
            }

            Debug.Log($"[WalkingCalibrationInputSystem] Restored calibration. Threshold={Threshold:F6}");
        }

        Debug.Log("Sensor ready. Press start button to begin calibration.");
    }

    private void Update()
    {
        if (!isCalibrating) return;
        if (IsCalibrated) return;
        if (linearAccelerationSensor == null) return;

        Vector3 acceleration = linearAccelerationSensor.acceleration.ReadValue();

        float currentSensorValue = GetLateralAcceleration(acceleration);

        AddSensorValue(currentSensorValue);
    }

    /// <summary>
    /// UIのスタートボタンから呼ぶ
    /// </summary>
    public void StartCalibration()
    {
        Debug.Log("Calibration started.");
        AnalyticsLogger.Event("calibration_start");

        isCalibrating = true;
        IsCalibrated = false;

        Threshold = 0.0f;
        Mean = 0.0f;
        Std = 0.0f;

        timeList.Clear();
        rawList.Clear();
        smoothedList.Clear();

        startTime = Time.time;
    }

    private float GetLateralAcceleration(Vector3 acceleration)
    {
        if (useXAxis) return acceleration.x;
        if (useYAxis) return acceleration.y;
        if (useZAxis) return acceleration.z;

        return acceleration.x;
    }

    private void AddSensorValue(float currentSensorValue)
    {
        float currentTime = Time.time - startTime;

        timeList.Add(currentTime);
        rawList.Add(currentSensorValue);

        if (currentTime >= calibrationDuration)
        {
            CalculateThreshold();

            isCalibrating = false;
            IsCalibrated = true;

            // 次シーン向けに結果を保持
            HasPersistedCalibration = true;
            PersistedThreshold = Threshold;
            PersistedMean = Mean;
            PersistedStd = Std;

            Debug.Log("Calibration finished.");
            Debug.Log($"Mean      : {Mean:F6}");
            Debug.Log($"Std       : {Std:F6}");
            Debug.Log($"Threshold : {Threshold:F6}");
            Debug.Log($"Sample count: {rawList.Count}");

            AnalyticsLogger.Event("calibration_finish", new Dictionary<string, object>
            {
                {"threshold", Threshold},
                {"mean",Mean},
                {"std", Std},
                {"samples",rawList.Count},
            });
        }
    }

    private void CalculateThreshold()
    {
        if (rawList.Count == 0)
        {
            Debug.LogWarning("No calibration data.");
            return;
        }

        smoothedList.Clear();

        float pastSmoothedValue = rawList[0];
        smoothedList.Add(pastSmoothedValue);

        for (int i = 1; i < rawList.Count; i++)
        {
            float currentSensorValue = rawList[i];

            float currentSmoothed =
                alpha * currentSensorValue
                + (1.0f - alpha) * pastSmoothedValue;

            smoothedList.Add(currentSmoothed);

            pastSmoothedValue = currentSmoothed;
        }

        float clipStartTime = clipDuration;
        float clipEndTime = calibrationDuration - clipDuration;

        List<float> clippedValues = new List<float>();

        for (int i = 0; i < smoothedList.Count; i++)
        {
            float t = timeList[i];

            if (t < clipStartTime) continue;
            if (t > clipEndTime) continue;

            float value = Mathf.Abs(smoothedList[i]);
            clippedValues.Add(value);
        }

        if (clippedValues.Count == 0)
        {
            Debug.LogWarning("No clipped data. Check calibrationDuration and clipDuration.");
            return;
        }

        Mean = CalculateMean(clippedValues);
        Std = CalculateStd(clippedValues, Mean);

        Threshold = Mean + thresholdScale * Std;

        if (interfaceClient != null) interfaceClient.SetWalkingThreshold(Threshold);
    }

    private float CalculateMean(List<float> values)
    {
        float sum = 0.0f;

        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / values.Count;
    }

    private float CalculateStd(List<float> values, float mean)
    {
        float varianceSum = 0.0f;

        for (int i = 0; i < values.Count; i++)
        {
            float diff = values[i] - mean;
            varianceSum += diff * diff;
        }

        float variance = varianceSum / values.Count;
        return Mathf.Sqrt(variance);
    }

    public bool IsCalibrating()
    {
        return isCalibrating;
    }

    public static void ClearPersistedCalibration()
    {
        HasPersistedCalibration = false;
        PersistedThreshold = 0.0f;
        PersistedMean = 0.0f;
        PersistedStd = 0.0f;
    }
}
