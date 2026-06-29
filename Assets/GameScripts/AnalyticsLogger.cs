using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Text;
using System;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class AnalyticsLogger : MonoBehaviour
{
    public static AnalyticsLogger Instance { get; private set;}
    [Header("送信先(API Gateway)")]
    public string endpointUrl = "";
    public string sharedSecret = "";

    [Header("送信設定")]
    public float flushIntervalSec = 30f;
    public int flushThreshold = 50;
    public bool wifiOnly = false;
    public bool captureLogs = true;

    private string sessionId;
    private string deviceId;
    private string currentScene = "";
    private readonly List<string> buffer = new List<string>();
    private readonly object bufferLock = new object();
    private bool sending = false;
    private float timer = 0f;

    void Awake() {
        //シングルトン化
        if (Instance != null) {Destroy(gameObject); return;}
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = Guid.NewGuid().ToString("N").Substring(0,12);
        deviceId = SystemInfo.deviceModel + "/" + SystemInfo.deviceUniqueIdentifier;
        currentScene = SceneManager.GetActiveScene().name;
        SceneManager.activeSceneChanged += OnSceneChanged;

        if (captureLogs)
            {Application.logMessageReceivedThreaded += OnUnityLog;}
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Log("app_start", new Dictionary<string, object> {
            {"platform", Application.platform.ToString()},
            {"version", Application.version},
        });
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        if (captureLogs)
            {Application.logMessageReceivedThreaded -= OnUnityLog;}
    }

    private void OnSceneChanged(Scene from, Scene to)
    {
        currentScene = to.name;
        Log("scene_enter", new Dictionary<string, object> {{"scene", to.name}});
    }

    // 公開API
    public static void Event(string type, Dictionary<string, object> data = null)
    {
        if (Instance != null) Instance.Log(type, data);
    }

    public void Log(string type, Dictionary<string, object> data = null)
    {
        string line = BuildLine(type, data);
        lock (bufferLock) {buffer.Add(line);}
    }

    private string BuildLine(string type, Dictionary<string, object> data)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"ts\":").Append(Json(DateTime.UtcNow.ToString("o"))).Append(',');
        sb.Append("\"session\":").Append(Json(sessionId)).Append(',');
        sb.Append("\"device\":").Append(Json(deviceId)).Append(',');
        sb.Append("\"scene\":").Append(Json(currentScene)).Append(',');
        sb.Append("\"type\":").Append(Json(type)).Append(',');
        sb.Append("\"data\":").Append(Json(data ?? new Dictionary<string, object>()));
        sb.Append('}');
        return sb.ToString();
    }

    private void OnUnityLog(string condition, string stackTrace, LogType logType)
    {
        if(logType == LogType.Log) return;
        Log("log", new Dictionary<string, object>
        {
            {"level", logType.ToString()},
            {"message", condition},
        });
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.unscaledDeltaTime;
        int count;
        lock (bufferLock) {count = buffer.Count; }
        if (count > 0 && (timer >= flushIntervalSec || count >= flushThreshold))
        {
            timer = 0f;
            Flush();
        }
    }

    private void Flush()
    {
        if (sending) return;
        if (string.IsNullOrEmpty(endpointUrl)) return;
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        if (wifiOnly && Application.internetReachability != NetworkReachability.ReachableViaLocalAreaNetwork) return;

        string payload;
        lock (bufferLock)
        {
            if (buffer.Count == 0) return;
            payload = string.Join("\n", buffer);
            buffer.Clear();
        }
        StartCoroutine(Send(payload));
    }

    private IEnumerator Send(string ndjson)
    {
        sending = true;
        byte[] body = Encoding.UTF8.GetBytes(ndjson);
        using (var req = new UnityWebRequest(endpointUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-ndjson");
            req.SetRequestHeader("x-api-secret", sharedSecret);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                lock (bufferLock) {buffer.Insert(0, ndjson); }
                Debug.LogWarning("[AnalyticsLogger] send Failed: "+req.error);
            }
        }
        sending = false;
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) Flush();
    }

    void OnApplicationQuit()
    {
        Flush();
    }

    // ===== 依存なしの最小JSONシリアライザ =====
    private static string Json(object v)
      {
          switch (v)
          {
              case null: return "null";
              case bool b: return b ? "true" : "false";
              case string s: return "\"" + Escape(s) + "\"";
              case int i: return i.ToString(CultureInfo.InvariantCulture);
              case long l: return l.ToString(CultureInfo.InvariantCulture);
              case float f: return f.ToString(CultureInfo.InvariantCulture);
              case double d: return d.ToString(CultureInfo.InvariantCulture);
              case IDictionary<string, object> dict:
              {
                  var sb = new StringBuilder("{");
                  bool first = true;
                  foreach (var kv in dict)
                  {
                      if (!first) sb.Append(',');
                      first = false;
                      sb.Append('"').Append(Escape(kv.Key)).Append("\":").Append(Json(kv.Value));
                  }
                  return sb.Append('}').ToString();
              }
              case IEnumerable<object> arr:
              {
                  var sb = new StringBuilder("[");
                  bool first = true;
                  foreach (var item in arr)
                  {
                      if (!first) sb.Append(',');
                      first = false;
                      sb.Append(Json(item));
                  }
                  return sb.Append(']').ToString();
              }
              default: return "\"" + Escape(v.ToString()) + "\"";
          }
      }

      private static string Escape(string s)
      {
          var sb = new StringBuilder();
          foreach (char c in s)
          {
              switch (c)
              {
                  case '"': sb.Append("\\\""); break;
                  case '\\': sb.Append("\\\\"); break;
                  case '\n': sb.Append("\\n"); break;
                  case '\r': sb.Append("\\r"); break;
                  case '\t': sb.Append("\\t"); break;
                  default:
                      if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                      else sb.Append(c);
                      break;
              }
          }
          return sb.ToString();
      }
}
