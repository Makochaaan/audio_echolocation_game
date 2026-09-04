using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// デバッグ用: 画面を3回連続タップしたら次のシーンへスキップする。
// WaitingScene / AdjustingScene / CalibrationScene の各シーンに置き、
// nextSceneName にそのシーンの遷移先を設定する。
public class TripleTapSkip : MonoBehaviour
{
    [Tooltip("スキップ時に遷移するシーン名")]
    [SerializeField] private string nextSceneName;

    [Tooltip("この秒数以内に次のタップが来ればカウント継続。超えたらリセット")]
    [SerializeField] private float maxTapInterval = 0.8f;

    [Tooltip("スキップに必要なタップ回数")]
    [SerializeField] private int requiredTaps = 3;

    [Tooltip("シーン遷移の直前に呼びたい処理（キャリブレーションのデフォルト値記録など）")]
    public UnityEvent onBeforeSkip;

    [SerializeField] private bool debugMode = true;

    private int tapCount = 0;
    private float lastTapTime = -999f;
    private bool hasSkipped = false;

    void Update()
    {
        if (hasSkipped) return;

        bool tapped =
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame); // エディタ確認用

        if (!tapped) return;

        // 前のタップから時間が空きすぎたら1回目から数え直す
        if (Time.unscaledTime - lastTapTime > maxTapInterval)
        {
            tapCount = 0;
        }

        tapCount++;
        lastTapTime = Time.unscaledTime;

        if (debugMode) Debug.Log($"[TripleTapSkip] tap {tapCount}/{requiredTaps}");

        if (tapCount >= requiredTaps)
        {
            hasSkipped = true;
            if (debugMode) Debug.Log($"[TripleTapSkip] Skipping to {nextSceneName}");
            onBeforeSkip?.Invoke();
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
