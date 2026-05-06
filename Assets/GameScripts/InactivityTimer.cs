using UnityEngine;
using UnityEngine.SceneManagement;

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
        if (Input.anyKey || Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
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
