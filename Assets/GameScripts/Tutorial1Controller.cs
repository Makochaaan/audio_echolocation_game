using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class Tutorial1Controller : MonoBehaviour
{
    [Header("連携オブジェクト")]
    public Transform player;
    public GameObject tutorialWall;
    public GameObject catGoal;

    [Tooltip("画面に指示を表示するテキストUI")]
    public TextMeshProUGUI instructionText;

    [Tooltip("ナレーションを再生するオーディオソース")]
    public AudioSource narratorSource;

    [Tooltip("ナレーション音声")]
    public AudioClip[] narrationClips;

    private Vector3 lastPos;
    private Quaternion lastRot;
    private int turnCount = 0;

    void Start()
    {
        tutorialWall.SetActive(false);
        if(catGoal != null) catGoal.SetActive(false);

        lastPos = player.position;
        lastRot = player.rotation;

        StartCoroutine(TutorialRoutine());
    }

    // Update is called once per frame
    void Update()
    {
        // 回転を取得
        if (Quaternion.Angle(lastRot, player.rotation) > 45f)
        {
            turnCount++;
            lastRot = player.rotation;
        }
    }

    // 前進して壁にぶつかるかを判定
    bool CheckBumpAttempt()
    {
        float moveVertical = Input.GetAxis("Vertical");
        float moveHorizontal = Input.GetAxis("Horizontal");

        if (moveVertical != 0 || moveHorizontal != 0)
        {
            Vector3 inputDir = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
            if (Vector3.Angle(player.forward, inputDir) < 1.0f)
            {
                Vector3 rayOrigin = player.position + Vector3.up * 0.1f;
                if (Physics.Raycast(rayOrigin, inputDir, 1.0f)) return true;
            }
        }
        return false;
    }

    IEnumerator TutorialRoutine()
    {
        // --- ステップ1 ---
        PlayInstruction("障害物が何もない空間です。\n一歩進んで足音（発信音）を鳴らしながら、その場で4回転してみてください。", narrationClips[0]);
        turnCount = 0;
        while (turnCount < 4) yield return null; // 4回方向転換するまで待つ
        yield return StartCoroutine(WaitForFacingStable());

        // --- ステップ2 ---
        // プレイヤーの3マス前に壁を出現させる
        Vector3 front = GetGridDir(player.forward);
        Vector3 basePos = GetGridPos(player.position);
        tutorialWall.transform.position = basePos + front * 3f;
        tutorialWall.SetActive(true);

        PlayInstruction("前方に壁を置きました。\n足音を鳴らしながら、そのまま前へ進んでみてください。", narrationClips[1]);

        // 壁にぶつかるまで待機
        bool hasBumped = false;
        while (!hasBumped)
        {
            if (CheckBumpAttempt())
            {
                hasBumped = true;
                yield return new WaitForSeconds(1.0f); // ぶつかる処理(PlayerController)が終わるのを少し待つ
            }
            yield return null;
        }

        // --- ステップ3 ---
        // 壁を右横(3マス先)に移動
        yield return StartCoroutine(WaitForFacingStable());
        Vector3 right = GetGridDir(player.right);
        tutorialWall.transform.position = GetGridPos(player.position) + right * 3f;
        
        PlayInstruction("壁にぶつかると音がします。\n次は壁を違う場所に移動しました。4回転して、どこに壁があるか音で判別してみてください。", narrationClips[2]);
        turnCount = 0;
        while (turnCount < 4) yield return null;

        // --- ステップ4 ---
        PlayInstruction("それでは、猫を探してみましょう。", narrationClips[3]);
        if(catGoal != null) 
        {
            catGoal.transform.position = player.position + player.forward * 5f;
            catGoal.SetActive(true);
        }

        // ここを「チュートリアル完了」とするなら遷移
        yield return new WaitForSeconds(10.0f); // １０秒自由に動くまで演出待ち
        SceneManager.LoadScene("2DScene");
    }
    void PlayInstruction(string text, AudioClip clip)
    {
        if (instructionText != null) instructionText.text = text;
        if (narratorSource != null && clip != null) narratorSource.PlayOneShot(clip);
    }

    Vector3 GetGridDir(Vector3 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.z)) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
        return new Vector3(0f, 0f, Mathf.Sign(v.z));
    }

    Vector3 GetGridPos(Vector3 p)
    {
        return new Vector3(Mathf.Round(p.x), 0.5f, Mathf.Round(p.z));
    }

    IEnumerator WaitForFacingStable()
    {
        Quaternion prev = player.rotation;
        int stable = 0;

        while (stable < 5)
        {
            yield return null;
            float delta = Quaternion.Angle(prev, player.rotation);
            prev = player.rotation;

            bool input = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
            stable = !input && delta < 0.05f ? stable + 1 : 0;
        }
    }
}
