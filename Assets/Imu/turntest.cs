using UnityEngine;
using UnityEngine.InputSystem;

public class turntest : MonoBehaviour
{
    [SerializeField] private PicoTurnClient turnReceiver;

    private void Update()
    {
        // Enterキーを押したときに1件だけ取り出して確認
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            string turn = turnReceiver.GetTurnState();
            Debug.Log("GetTurnState() -> " + turn);
        }
    }
}
