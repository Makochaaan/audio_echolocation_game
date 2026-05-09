using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class InactivityTimer : MonoBehaviour
{
    [Header("タイムアウト設定")]
    [Tooltip("無操作がこの秒数続くとスタートシーンに戻ります")]
    public float timeoutSeconds = 60f; // デフォルトは60秒（1分）

    private float timer = 0f;

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
            SceneManager.LoadScene("StartScene");
        }
    }
}
