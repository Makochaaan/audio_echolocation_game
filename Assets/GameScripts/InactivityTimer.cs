using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class InactivityTimer : MonoBehaviour
{
    [Header("タイムアウト設定")]
    [Tooltip("無操作がこの秒数続くとスタートシーンに戻ります")]
    public float timeoutSeconds = 60f; // デフォルトは60秒（1分）

    [Header("IMU/移動検知")]
    [Tooltip("IMU移動などの判定に使うプレイヤーのTransform")]
    public Transform playerTransform;
    [Tooltip("この距離以上の移動を入力とみなす")]
    public float movementThreshold = 0.05f;
    [Tooltip("この角度以上の回転を入力とみなす")]
    public float rotationThreshold = 5f;

    private float timer = 0f;
    private Vector3 lastPos;
    private Quaternion lastRot;

    void Start()
    {
        if (playerTransform != null)
        {
            lastPos = playerTransform.position;
            lastRot = playerTransform.rotation;
        }
    }

    void Update()
    {
        // 何らかのキーボード入力、または移動操作があったかチェック
        // Input.anyKey はキーが押されている間ずっと反応します
        bool hasInput = false;
        
        // キーボード入力をチェック
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
        {
            hasInput = true;
        }
        
        // ゲームパッド入力をチェック
        if (!hasInput && Gamepad.current != null)
        {
            var movement = Gamepad.current.leftStick.ReadValue();
            if (movement.magnitude > 0.1f)
            {
                hasInput = true;
            }
        }

        // IMU移動や回転を入力として扱う
        if (!hasInput && playerTransform != null)
        {
            float moveDelta = Vector3.Distance(playerTransform.position, lastPos);
            float rotDelta = Quaternion.Angle(lastRot, playerTransform.rotation);
            if (moveDelta >= movementThreshold || rotDelta >= rotationThreshold)
            {
                hasInput = true;
            }
            lastPos = playerTransform.position;
            lastRot = playerTransform.rotation;
        }
        
        if (hasInput)
        {
            // 入力があればタイマーをリセット
            timer = 0f; 
        }
        else
        {
            // 入力がなければタイマーを進める
            timer += Time.deltaTime; 
        }

        // タイムアウト時間を超えたらスタートシーンへ
        if (timer >= timeoutSeconds)
        {
            SceneManager.LoadScene("1. WaitingScene");
        }
    }
}
