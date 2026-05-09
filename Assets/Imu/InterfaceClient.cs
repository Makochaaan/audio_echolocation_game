using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InterfaceClient : MonoBehaviour
{
    //センサの値を保存しておく変数
    private Vector3 linearAccel;
    private Vector3 angularVelocity;

    //角度(rad)を保存する変数と角速度を積分するために時間を記録する変数
    //角度は取得する際にはdegreeに変換する
    private float yaw;
    private float lastMs;

    private float baseYawDeg;
    //yawの分散を計算するために時系列データを記録するリスト、MAXLEN個記録する
    private List<float> yawList = new List<float>();
    [SerializeField] private int MAXLEN = 50;
    [SerializeField] private float turnThreshold = 60.0f;
    [SerializeField] private float resetThreshold = 0.5f;
    //歩数検知のための係数設定項目
    [SerializeField] private float alpha = 0.1f;
    //歩数をカウントする閾値
    [SerializeField] private float walkingThredhold = 0.04f;
    
    [SerializeField] private bool debugMode = false;

    //turn判定を記録しておく変数
    //右が0,左が1
    private readonly Queue<int> turnHistory = new Queue<int>();
    private int LEFTTURNSTATE = 1;
    private int RIGHTTURNSTATE = 0;

    //歩数をカウントする変数
    private int stepCount = 0;
    
    //閾値を超えたときに一回だけ歩数判定ができるようにするために必要な変数
    private bool readyToJudge = false;

    //読み取ったy軸方向の加速度と平滑化するための変数
    private float currentValue = 0.0f;
    private float postSmoothedValue = 0.0f;
    private float currentSmoothedValue = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //initialize
        yaw = 0.0f;
        lastMs = Time.realtimeSinceStartup;

        // シーン遷移後でもキャリブレーション済み閾値を引き継ぐ
        if (WalkingCalibrationInputSystem.HasPersistedCalibration)
        {
            SetWalkingThreshold(WalkingCalibrationInputSystem.PersistedThreshold);
            if (debugMode)
            {
                Debug.Log($"[InterfaceClient] Restored walking threshold: {WalkingCalibrationInputSystem.PersistedThreshold:F6}");
            }
        }

        //enable sensors
        if (LinearAccelerationSensor.current != null)
            InputSystem.EnableDevice(LinearAccelerationSensor.current);

        if (UnityEngine.InputSystem.Gyroscope.current != null)
            InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);

        if (AttitudeSensor.current != null)
            InputSystem.EnableDevice(AttitudeSensor.current);
    }

    // Update is called once per frame
    void Update()
    {
        if (LinearAccelerationSensor.current == null || UnityEngine.InputSystem.Gyroscope.current == null)
        {
            return;
        }
        //sensor values update
        linearAccel = LinearAccelerationSensor.current.acceleration.ReadValue();
        angularVelocity = UnityEngine.InputSystem.Gyroscope.current.angularVelocity.ReadValue();

        //yawの値を取得して記録
        float nowYawDeg = GetYawDeg();
        yawList.Add(nowYawDeg);
        //　要素数が超えていたら削除
        if (yawList.Count > MAXLEN) yawList.RemoveAt(0);

        //turn判定部分
        //右が0,左が1
        if (nowYawDeg - baseYawDeg > turnThreshold)
        {
            baseYawDeg = nowYawDeg;
            turnHistory.Enqueue(LEFTTURNSTATE);
            if (debugMode)
            {
                Debug.Log(string.Join(", ", turnHistory));
            }
        }

        if (nowYawDeg - baseYawDeg < -turnThreshold)
        {
            baseYawDeg = nowYawDeg;
            turnHistory.Enqueue(RIGHTTURNSTATE);
            if (debugMode)
            {
                Debug.Log(string.Join(", ", turnHistory));
            }
        }

        if (Variance(yawList) < resetThreshold)
        {
            baseYawDeg = nowYawDeg;
        }

        //平滑化
        currentValue = linearAccel.y;
        currentSmoothedValue = alpha * currentValue + (1 - alpha) * postSmoothedValue;
        postSmoothedValue = currentSmoothedValue;

        //平滑化後の値を歩数判定
        if (Mathf.Abs(currentSmoothedValue) > walkingThredhold && readyToJudge)
        {
            stepCount++;
            readyToJudge = false;
            if (debugMode)
            {
                Debug.Log($"stepCount: {stepCount}");
            }
        }

        //歩いた判定エリア外に出たら歩いた判定開始
        if (Mathf.Abs(currentSmoothedValue) < walkingThredhold)
        {
            readyToJudge = true;
        }

        
    }

    private float GetYawDeg()
    {
        float nowMs = Time.realtimeSinceStartup;
        float dt = nowMs - lastMs;
        lastMs = nowMs;
    
        yaw += angularVelocity.z * dt;
        // return degrees
        return Mathf.Rad2Deg * yaw;
    }

    private float Variance(List<float> data)
    {
        int n = data.Count;

        if (n == 0)
        {
            return 0.0f;
        }

        float s = 0.0f;

        for (int i = 0; i < n; i++)
        {
            s += data[i];
        }

        float mean = s / n;
        
        float variance = 0f;

        for (int i = 0; i < n; i++)
        {
            float diff = data[i] - mean;
            variance += diff*diff;
        }
        return variance / n;
    }

    /// <summary>
    /// 曲がった情報を取得できる関数
    /// 曲がった状態履歴は蓄積されて、この関数が実行されたら最も古い履歴が消されます
    /// 右なら0、左なら1、履歴が全てない場合は-1が返される
    /// </summary>
    /// <returns>0 (right),  1 (left),  -1 (yet no turn state)  </returns>
    public int GetTurnState()
    {
        if (turnHistory.Count > 0)
        {
            return turnHistory.Dequeue(); // 最も古い履歴を返して削除
        }

        return -1;
    }
    /// <summary>
    /// 歩数を取得する関数
    /// 使い方としては、過去の歩数を取得しておいて、現在の値と差し引きすることでどれだけ歩数が進んだかを表示するといった感じ
    /// </summary>
    /// <returns>歩数(integer)</returns>
    public int GetStepCount()
    {
        return stepCount;
    }
    /// <summary>
    /// WalkingCalibrationInputSystemから利用される関数
    /// キャリブレーション後の歩数判定の閾値が代入されます
    /// キャリブレーション前ではプログラム作成者のキャリブレーション時の値が入っています
    /// </summary>
    /// <param name="thredhold"></param>
    public void SetWalkingThreshold(float thredhold)
    {
        walkingThredhold = thredhold;
    }
}
