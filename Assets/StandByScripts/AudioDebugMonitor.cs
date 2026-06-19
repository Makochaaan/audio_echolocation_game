using UnityEngine;
using System.Text;

// 指定した AudioSource が「内部的に実際に音を出しているか」を確認するデバッグ用モニタ。
//
// isPlaying（再生命令が通ったか）だけでなく、GetOutputData で取得した
// 実際の出力波形レベル(RMS)も表示する。これにより
//   ・isPlaying=true なのに sourceRMS≒0  → クリップが無音 / volume / mute
//   ・sourceRMS>0 なのに listenerRMS≒0   → 空間化やミキサー、Listener 側で消えている
//   ・isPlaying=false                     → そもそも Play() が効いていない
//   ・loadState=Failed                    → クリップのインポート/ロード失敗
// のように「どこで音が消えているか」を切り分けられる。
//
// 使い方: 音を確認したい AudioSource と同じ GameObject に付けるか、target に割り当てる。
public class AudioDebugMonitor : MonoBehaviour
{
    [Tooltip("監視する AudioSource。未指定なら同じ GameObject から取得")]
    public AudioSource target;

    [Tooltip("ログ出力の間隔（秒）。0なら毎フレーム")]
    public float logInterval = 0.5f;

    [Tooltip("Debug.Log に出力する（logcat 用）")]
    public bool logToConsole = true;

    [Tooltip("画面にオーバーレイ表示する（実機で目視確認用）")]
    public bool showOnScreen = true;

    [Tooltip("オーバーレイの縦位置スロット。複数付けるとき重ならないよう 0,1,2... と分ける")]
    public int panelSlot = 0;

    [Tooltip("AudioListener 全体の最終出力レベルも見る（スピーカーへ実際に出ているか）")]
    public bool monitorListener = true;

    private float lastLogTime = -999f;
    private readonly float[] sampleBuffer = new float[256];
    private string status = "";

    void Awake()
    {
        if (target == null) target = GetComponent<AudioSource>();
    }

    void Update()
    {
        status = BuildStatus();

        if (logToConsole && (logInterval <= 0f || Time.time - lastLogTime >= logInterval))
        {
            Debug.Log("[AudioDebug] " + status.Replace("\n", " | "));
            lastLogTime = Time.time;
        }
    }

    string BuildStatus()
    {
        var sb = new StringBuilder();

        if (target == null)
        {
            sb.AppendLine("target = NULL（AudioSource 未割り当て）");
        }
        else
        {
            string clipName = target.clip != null ? target.clip.name : "NONE";
            string loadState = target.clip != null ? target.clip.loadState.ToString() : "-";
            float len = target.clip != null ? target.clip.length : 0f;
            // clip が無い（resource が clip でない）状態で .time を読むと
            // Unity が警告を出すため、clip があるときだけ読む
            float t = target.clip != null ? target.time : 0f;

            sb.AppendLine($"src='{target.gameObject.name}'  clip={clipName} ({loadState})");
            sb.AppendLine($"isPlaying={target.isPlaying}  time={t:F2}/{len:F2}");
            sb.AppendLine($"vol={target.volume:F2} mute={target.mute} blend={target.spatialBlend:F2} spatialize={target.spatialize}");
            sb.AppendLine($"sourceRMS={GetRms(false):F5}");
        }

        if (monitorListener)
        {
            sb.AppendLine($"listenerRMS={GetRms(true):F5}  listenerVol={AudioListener.volume:F2} paused={AudioListener.pause}");
        }

        return sb.ToString();
    }

    // fromListener=false: この AudioSource の出力レベル
    // fromListener=true : AudioListener（最終ミックス）の出力レベル
    float GetRms(bool fromListener)
    {
        if (!fromListener && (target == null || !target.isPlaying)) return 0f;

        if (fromListener) AudioListener.GetOutputData(sampleBuffer, 0);
        else target.GetOutputData(sampleBuffer, 0);

        float sum = 0f;
        for (int i = 0; i < sampleBuffer.Length; i++)
        {
            sum += sampleBuffer[i] * sampleBuffer[i];
        }
        return Mathf.Sqrt(sum / sampleBuffer.Length);
    }

    void OnGUI()
    {
        if (!showOnScreen) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 26,
            wordWrap = true
        };
        style.normal.textColor = Color.green;

        const float panelHeight = 250f;
        float y = 10f + panelSlot * (panelHeight + 10f);
        GUI.Box(new Rect(10, y, Mathf.Min(Screen.width - 20, 900), panelHeight), status, style);
    }
}
