using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PicoTurnClient : MonoBehaviour
{
    [SerializeField] private string serverIp = "192.168.11.5"; // Pico/ESP 側のIP
    [SerializeField] private float pollInterval = 0.1f;          // 何秒ごとに取得するか

    private readonly Queue<string> turnHistory = new Queue<string>();

    private string TurnUrl => $"http://{serverIp}/turn";

    private void Start()
    {
        StartCoroutine(PollTurnState());
    }

    private IEnumerator PollTurnState()
    {
        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(TurnUrl))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool success = request.result == UnityWebRequest.Result.Success;
#else
                bool success = !request.isNetworkError && !request.isHttpError;
#endif

                if (success)
                {
                    string turnState = request.downloadHandler.text.Trim().ToLower();

                    if (turnState == "left" || turnState == "right")
                    {
                        turnHistory.Enqueue(turnState);
                        Debug.Log($"turn received: {turnState}");
                    }
                }
                else
                {
                    Debug.LogWarning($"TurnState request failed: {request.error}");
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    public string GetTurnState()
    {
        if (turnHistory.Count > 0)
        {
            return turnHistory.Dequeue(); // 最も古い履歴を返して削除
        }

        return "none";
    }

    public int GetTurnHistoryCount()
    {
        return turnHistory.Count;
    }
}