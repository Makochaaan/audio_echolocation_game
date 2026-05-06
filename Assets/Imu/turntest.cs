using UnityEngine;

public class turntest : MonoBehaviour
{
    [SerializeField] private PicoTurnClient turnReceiver;

    private void Update()
    {
        // Enterキーを押したときに1件だけ取り出して確認
        if (Input.GetKeyDown(KeyCode.Return))
        {
            string turn = turnReceiver.GetTurnState();
            Debug.Log("GetTurnState() -> " + turn);
        }
    }
}