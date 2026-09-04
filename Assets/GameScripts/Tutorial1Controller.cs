using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Tutorial1Controller : MonoBehaviour
{
    [Header("連携オブジェクト")]
    public Transform player;
    public GameObject tutorialWall;
    public GameObject catGoal;
    [Tooltip("チュートリアル用の猫コントローラー")]
    public TutorialCatGoalController tutorialCatController;

    [Tooltip("画面に指示を表示するテキストUI")]
    public TextMeshProUGUI instructionText;

    [Tooltip("ナレーションを再生するオーディオソース")]
    public AudioSource narratorSource;

    [Tooltip("ナレーション音声")]
    public AudioClip[] narrationClips;

    [Header("判定設定")]
    [Tooltip("この距離以内まで近づいて前進入力したときに『ぶつかった』と判定する")]
    public float bumpDetectDistance = 0.55f;

    private Vector3 lastPos;
    private Quaternion lastRot;
    private int turnCount = 0;
    private int moveCount = 0;
    private bool isNarrationPlaying = false;
    private PlayerController2D playerController2D;

    void Start()
    {
        tutorialWall.SetActive(false);
        
        // チュートリアル用の猫コントローラーを使用
        if(tutorialCatController != null)
        {
            tutorialCatController.Hide();
        }
        else if(catGoal != null)
        {
            catGoal.SetActive(false);
        }

        lastPos = player.position;
        lastRot = player.rotation;

        if (player != null)
        {
            playerController2D = player.GetComponent<PlayerController2D>();
        }

        StartCoroutine(TutorialRoutine());
    }

    // Update is called once per frame
    void Update()
    {
        // ナレーション中は操作を受け付けない
        if (isNarrationPlaying) return;

        // 回転を取得
        if (Quaternion.Angle(lastRot, player.rotation) > 45f)
        {
            turnCount++;
            lastRot = player.rotation;
            TriggerCatMeowOnAction();
        }

        // 移動を取得（グリッドサイズ分移動したかを検知）
        if (Vector3.Distance(lastPos, player.position) > 0.5f)
        {
            moveCount++;
            lastPos = player.position;
            TriggerCatMeowOnAction();
        }
    }

    // 前進して壁にぶつかるかを判定
    bool CheckBumpAttempt()
    {
        // ナレーション中は操作を受け付けない
        if (isNarrationPlaying) return false;

        if (playerController2D != null && playerController2D.ConsumeBumpSignal())
        {
            return true;
        }

        if (Keyboard.current == null) return false;

        float moveHorizontal = 0f;
        float moveVertical = 0f;

        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
        {
            moveHorizontal = -1f;
        }
        else if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
        {
            moveHorizontal = 1f;
        }

        if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
        {
            moveVertical = -1f;
        }
        else if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
        {
            moveVertical = 1f;
        }

        if (moveVertical != 0 || moveHorizontal != 0)
        {
            Vector3 inputDir = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
            if (Vector3.Angle(player.forward, inputDir) < 1.0f)
            {
                Vector3 rayOrigin = player.position + Vector3.up * 0.1f;
                if (Physics.Raycast(rayOrigin, inputDir, out RaycastHit hit, 1.0f))
                {
                    // 壁のかなり手前では反応させず、接触直前でのみ判定する
                    if (hit.distance <= bumpDetectDistance) return true;
                }
            }
        }
        return false;
    }

    IEnumerator TutorialRoutine()
    {
        // --- ステップ1 ---
        // ナレーション前にプレイヤーを中心とした7×7の部屋を設置
        Vector3 startPos = GetGridPos(player.position);
        BuildRoom(startPos);

        yield return StartCoroutine(PlayInstruction("まっすぐ歩いてみてください。", narrationClips[0]));
        // 2歩進んだら次のナレーションへ
        moveCount = 0;
        while (moveCount < 2) yield return null;

        yield return StartCoroutine(PlayInstruction("前方に壁を置きました。その壁に向かって、歩いてみてください。", narrationClips[1]));
        
        // 壁にぶつかるまで待機
        bool hasBumped = false;
        while (!hasBumped)
        {
            if (CheckBumpAttempt())
            {
                hasBumped = true;
                yield return new WaitForSeconds(1.0f); // ぶつかる処理が終わるのを少し待つ
            }
            yield return null;
        }

        yield return StartCoroutine(WaitForFacingStable());

        yield return StartCoroutine(PlayInstruction("壁にぶつかりました。（ドン）壁にぶつかると、進めません。壁へ近づくにつれて、足音が少しずつ変わっていったことに気付きましたか？", narrationClips[2]));

        // --- ステップ2 ---
        // テキスト：回転を体験する。回転カウンターを使用して2回の回転を待つ
        yield return StartCoroutine(PlayInstruction("手すりを持ったまま、身体ごと横に向きを変えてください。", narrationClips[3]));
        
        // 2回の回転を待機
        turnCount = 0;
        while (turnCount < 1) yield return null;

        yield return StartCoroutine(PlayInstruction("向きが変わりました。向きを変えると、進める方向が変わります。ゲームの中では90度づつ曲がることができます。もう一度、同じ向きで、向きを変えてみてください。", narrationClips[4]));

        // ★このステップ完了後に 2DScene へ遷移する（以降のチュートリアル手順はスキップ）
        SceneManager.LoadScene("2DScene");
        yield break;

        while (turnCount < 2) yield return null;

        yield return StartCoroutine(PlayInstruction("歩いてください。", narrationClips[5]));
        moveCount = 0;
        while (moveCount < 1) yield return null;

        
        yield return StartCoroutine(WaitForFacingStable());

        // --- ステップ3 ---
        // 壁を目の前の3マス先に配置し、その裏1マス目に猫を配置
        Vector3 forwardDir = GetGridDir(player.forward);
        tutorialWall.transform.position = GetGridPos(player.position) + forwardDir * 4f;
        tutorialWall.tag = "Container";
        
        // チュートリアル用の猫を配置
        if(tutorialCatController != null)
        {
            tutorialCatController.SetPosition(GetGridPos(player.position) + forwardDir * 5f);
            tutorialCatController.Show();
        }
        else if(catGoal != null)
        {
            catGoal.transform.position = GetGridPos(player.position) + forwardDir * 5f;
            catGoal.SetActive(true);
        }
        
        yield return StartCoroutine(PlayInstruction("おや？　どこからか、猫の声が聞こえてきました。猫の方へ向かって、歩いてみましょう。", narrationClips[6]));

        // 移動を2回待つ
        moveCount = 0;
        while (moveCount < 2) yield return null;

        // --- ステップ4 ---
        // ナレーション：猫を捕まえるよう指示
        yield return StartCoroutine(PlayInstruction("猫とあなたの間には、1マス分の壁があるようです。　１マス横に歩いて、壁の裏側に回り込み、猫をつかまえてみましょう！", narrationClips[7]));

        // タイマー付きで猫の捕捉を待機（制限時間30秒）
        float timeLimit = 90f;
        float timer = 0f;
        bool catCaught = false;

        while (timer < timeLimit && !catCaught)
        {
            // プレイヤーと猫の距離を確認
            Vector3 catPosition = Vector3.zero;
            bool catActive = false;

            if(tutorialCatController != null && tutorialCatController.gameObject.activeInHierarchy)
            {
                catPosition = tutorialCatController.transform.position;
                catActive = true;
            }
            else if(catGoal != null && catGoal.gameObject.activeInHierarchy)
            {
                catPosition = catGoal.transform.position;
                catActive = true;
            }

            if (catActive && Vector3.Distance(player.position, catPosition) < 1.5f)
            {
                catCaught = true;
                break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 結果に応じた分岐
        if (catCaught)
        {
            // 成功時のナレーション
            yield return StartCoroutine(PlayInstruction("みつけた！ 猫が近くにいます！（キャッチ音）以上でチュートリアルは完了です。", narrationClips[8]));
        }
        else
        {
            // 失敗時のナレーション（narrationClips[7]があれば使用、なければテキストのみ）
            if (narrationClips[7] != null)
            {
                yield return StartCoroutine(PlayInstruction("時間内に猫を見つけることができませんでした。次のステージで頑張ってください。", narrationClips[8]));
            }
            else
            {
                yield return StartCoroutine(PlayInstruction("時間内に猫を見つけることができませんでした。次のステージで頑張ってください。", null));
                yield return new WaitForSeconds(3f);
            }
        }

        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene("2DScene");
    }
    IEnumerator PlayInstruction(string text, AudioClip clip)
    {
        if (instructionText != null) instructionText.text = text;

        if (narratorSource != null && clip != null)
        {
            SetPlayerInputEnabled(false);
            isNarrationPlaying = true;
            narratorSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
            isNarrationPlaying = false;
            SetPlayerInputEnabled(true);
        }
        else
        {
            yield return null;
        }
    }

    void SetPlayerInputEnabled(bool enabled)
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = enabled;
        }

        PlayerController2D playerController2D = player.GetComponent<PlayerController2D>();
        if (playerController2D != null)
        {
            playerController2D.enabled = enabled;
            if (enabled)
            {
                playerController2D.ResetInputState();
            }
        }
    }

    void TriggerCatMeowOnAction()
    {
        if (tutorialCatController != null && tutorialCatController.gameObject.activeInHierarchy)
        {
            tutorialCatController.PlayMeow();
            return;
        }

        if (catGoal != null && catGoal.activeInHierarchy)
        {
                CatGoalController catController = catGoal.GetComponent<CatGoalController>();
                if (catController != null)
                {
                    catController.PlayMeow(true);
                }
        }
    }

    // tutorialWall を雛形に、center を中心とした7×7（内側5×5）の外周24マスへ壁を複製する。
    // 中央からはどの向きでも2歩進めて、3歩目で必ず壁にぶつかる
    void BuildRoom(Vector3 center)
    {
        const int half = 3;
        Transform room = new GameObject("TutorialRoom").transform;
        for (int dx = -half; dx <= half; dx++)
        {
            for (int dz = -half; dz <= half; dz++)
            {
                if (Mathf.Abs(dx) != half && Mathf.Abs(dz) != half) continue;
                GameObject wall = Instantiate(tutorialWall, center + new Vector3(dx, 0f, dz), Quaternion.identity, room);
                wall.tag = "Wall";
                wall.SetActive(true);
            }
        }
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

            bool input = Keyboard.current != null && (
                Keyboard.current.leftArrowKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed ||
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.dKey.isPressed ||
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.sKey.isPressed
            );
            stable = !input && delta < 0.05f ? stable + 1 : 0;
        }
    }
}
