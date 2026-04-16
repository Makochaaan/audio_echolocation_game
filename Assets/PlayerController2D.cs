using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController2D : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 0.2f; // 1マス移動・方向転換にかかる時間
    public float gridSize = 1.0f; // 1マスのサイズ

    [Header("足音設定")]
    public AudioClip groundSound; // 土の足音
    public AudioClip metalSound;  // 鉄板の足音

    [Header("回転音設定")]
    public AudioClip turnSound; // 回転したときの音

    [Header("壁衝突設定")]
    public AudioClip bumpSound; // ぶつかった瞬間の音（カンッという音など）
    [Tooltip("Wallタグにぶつかった時の衝突音。未設定なら bumpSound を使用")]
    public AudioClip wallBumpSound;
    [Tooltip("Containerタグにぶつかった時の衝突音。未設定なら bumpSound を使用")]
    public AudioClip containerBumpSound;
    
    [Header("持続ノイズ設定(コンテナ用)")]
    [Tooltip("Containerタグの障害物にぶつかった場所に残る持続音。")]
    public AudioClip sustainNoiseSound; 
    [Tooltip("ノイズ音が持続（ループ再生）するターン数（0なら鳴らない）")]
    public int sustainDurationTurns = 2; 

    [Tooltip("壁にぶつかった時もターンを消費して猫を動かすか")]
    public bool consumeTurnOnBump = true;
    [Tooltip("衝突音の連続生成を防ぐ最小間隔(秒)")]
    public float bumpSoundCooldown = 0.2f;

    [Header("連携設定")]
    [Tooltip("インスペクターから、対象となる猫(CatGoal)オブジェクトを割り当ててください")]
    public CatGoalController catGoal; // 猫のスクリプトへの参照
    [Tooltip("外部ネットワークからleft/rightを受け取るクライアント")]
    public PicoTurnClient turnReceiver;

    [Header("ゲーム成功演出")]
    [Tooltip("GoalCatの周囲1マスに入った時に再生する音")]
    public AudioClip goalSuccessSound;

    [Tooltip("ゲームクリア時のナレーション")]
    public AudioClip clearNarration;

    [Tooltip("成功演出後に戻るシーン名")]
    public string startSceneName = "StartScene";

    [Header("音量設定")]
    [Range(0f, 1f)] public float groundVolume = 1f;
    [Range(0f, 1f)] public float metalVolume = 1f;
    [Range(0f, 1f)] public float wallBumpVolume = 1f;
    [Range(0f, 1f)] public float containerBumpVolume = 1f;
    [Range(0f, 1f)] public float defaultBumpVolume = 1f;
    [Range(0f, 1f)] public float sustainNoiseVolume = 1f;
    [Range(0f, 1f)] public float goalSuccessVolume = 1f;
    [Range(0f, 1f)] public float clearNarrationVolume = 1f;
    [Range(0f, 1f)] public float manualSonarVolume = 1f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Echolocation echolocation; // 反響システムへの参照
    private bool isActing = false; 
    private bool hasGameEnded = false;
    private string currentGroundTag = "Untagged"; 
    private float nextAllowedBumpSoundTime = 0f;
    
    // 現在の経過ターン数を記録する変数
    private int currentTurnCount = 0; 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; 
        audioSource = GetComponent<AudioSource>();
        echolocation = GetComponent<Echolocation>();

        // 開始位置をグリッドにピッタリ合わせる
        transform.position = new Vector3(
            Mathf.Round(transform.position.x / gridSize) * gridSize,
            transform.position.y,
            Mathf.Round(transform.position.z / gridSize) * gridSize
        );
    }

    void Update()
    {
        if (isActing || hasGameEnded) return;

        // Spaceで任意にソナーを発動（反響音は打撃音を優先）
        if (Input.GetKeyDown(KeyCode.Space) && echolocation != null)
        {
            AudioClip manualSonarClip = bumpSound != null ? bumpSound : echolocation.echoSound;
            echolocation.TriggerSonarWithClip(manualSonarClip, manualSonarVolume);
            return;
        }

        // 1) ネットワークからの左右回転入力
        if (turnReceiver != null)
        {
            string turnState = turnReceiver.GetTurnState();
            if (turnState == "left" || turnState == "right")
            {
                Vector3 turnDirection = GetTurnDirectionFromState(turnState);
                StartCoroutine(TurnGrid(turnDirection));
                if (turnSound != null)
                {
                    audioSource.PlayOneShot(turnSound);
                }
                return;
            }
        }

        // 2) 矢印キー（Horizontal/Vertical）による従来操作
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");
        if (moveVertical != 0) moveHorizontal = 0;

        if (moveHorizontal != 0 || moveVertical != 0)
        {
            Vector3 inputDirection = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
            float angleDifference = Vector3.Angle(transform.forward, inputDirection);

            if (angleDifference < 1.0f)
            {
                StartCoroutine(MoveGrid(inputDirection));
            }
            else
            {
                StartCoroutine(TurnGrid(inputDirection));
            }
            return;
        }

        // 3) Enter（テンキー含む）で現在向いている方向へ1マス前進
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            StartCoroutine(MoveGrid(transform.forward));
        }
    }

    Vector3 GetTurnDirectionFromState(string turnState)
    {
        float yAngle = turnState == "left" ? -90f : 90f;
        return Quaternion.Euler(0f, yAngle, 0f) * transform.forward;
    }

    // ターンを消費した際の共通処理
    void EndTurn()
    {
        currentTurnCount++;
        if (catGoal != null) catGoal.TakeTurn();
    }

    IEnumerator MoveGrid(Vector3 direction)
    {
        isActing = true;

        Vector3 targetPosition = transform.position + direction * gridSize;

        // 壁・障害物判定
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, gridSize))
        {
            int turnCountAfterBumpAction = currentTurnCount + (consumeTurnOnBump ? 1 : 0);

            // ぶつかった対象のTagを渡して処理を行う
            if (Time.time >= nextAllowedBumpSoundTime)
            {
                StartCoroutine(HandleBump(hit.point, hit.collider.tag, turnCountAfterBumpAction));
                nextAllowedBumpSoundTime = Time.time + Mathf.Max(0f, bumpSoundCooldown);
            }

            if (consumeTurnOnBump)
            {
                EndTurn(); 
            }

            isActing = false;
            yield break; // 移動はキャンセル
        }

        // 床の材質をチェックして足音を鳴らす
        CheckGroundMaterial();
        PlayStepSound();
        
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        while (elapsedTime < moveTime)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null; 
        }
        transform.position = targetPosition;

        // 移動後の位置を基準にソナーを発動する
        if (echolocation != null)
        {
            echolocation.TriggerSonar();
        }

        if (IsPlayerWithinOneTileOfCat())
        {
            StartCoroutine(CatchCatSequence());
            yield break;
        }

        // 移動完了してターン消費
        EndTurn(); 

        isActing = false;
    }

    bool IsPlayerWithinOneTileOfCat()
    {
        if (catGoal == null || gridSize <= 0f)
        {
            return false;
        }

        Vector3 playerPos = transform.position;
        Vector3 catPos = catGoal.transform.position;

        float xTiles = Mathf.Abs(playerPos.x - catPos.x) / gridSize;
        float zTiles = Mathf.Abs(playerPos.z - catPos.z) / gridSize;
        // Debug.Log($"Player-Cat distance in tiles: x={xTiles}, z={zTiles}");

        return xTiles <= 1.501f && zTiles <= 1.501f;
    }

    IEnumerator TurnGrid(Vector3 direction)
    {
        isActing = true;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float elapsedTime = 0f;

        while (elapsedTime < moveTime)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;

        isActing = false;
    }

    // ★障害物にぶつかった時の処理
    IEnumerator HandleBump(Vector3 position, string hitTag, int turnCountAfterBumpAction)
    {
        // ぶつかった位置に、一時的な音源オブジェクトを作成する
        GameObject bumpAudioObj = new GameObject("BumpSound_Temp");
        bumpAudioObj.transform.position = position;
        AudioSource source = bumpAudioObj.AddComponent<AudioSource>();
        source.spatialBlend = 1.0f; // 立体音響
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.0f;
        source.maxDistance = 20.0f;

        AudioClip impactClip = GetImpactClipByTag(hitTag);
        float impactVolume = GetImpactVolumeByTag(hitTag);

        // 1. ぶつかった瞬間の音（タグ別）を1回鳴らす
        if (impactClip != null)
        {
            source.PlayOneShot(impactClip, impactVolume);
        }

        // 2. ぶつかった相手が「Container」タグであり、設定が有効な場合のみ持続音を鳴らす
        if (hitTag == "Container" && sustainNoiseSound != null && sustainDurationTurns > 0)
        {
            // 持続ノイズをループ再生状態にする
            source.clip = sustainNoiseSound;
            source.loop = true;
            source.volume = sustainNoiseVolume;
            source.Play();

            // 衝突行動後のターン数を基準に、指定ターン数だけ持続させる
            int targetTurn = turnCountAfterBumpAction + sustainDurationTurns;
            while (currentTurnCount < targetTurn)
            {
                yield return null;
            }
            
            // ターンが経過したら音を止める
            source.Stop();
        }
        else
        {
            // コンテナ以外（普通の壁）の場合は、衝突音が鳴り終わるまでだけ待つ
            if (impactClip != null) yield return new WaitForSeconds(impactClip.length);
        }

        // 一時オブジェクトを削除
        Destroy(bumpAudioObj);
    }

    AudioClip GetImpactClipByTag(string hitTag)
    {
        if (hitTag == "Container" && containerBumpSound != null)
        {
            return containerBumpSound;
        }

        if (hitTag == "Wall" && wallBumpSound != null)
        {
            return wallBumpSound;
        }

        return bumpSound;
    }

    float GetImpactVolumeByTag(string hitTag)
    {
        if (hitTag == "Container")
        {
            return containerBumpVolume;
        }

        if (hitTag == "Wall")
        {
            return wallBumpVolume;
        }

        return defaultBumpVolume;
    }

    void PlayStepSound()
    {
        if (audioSource == null) return;
        
        if (currentGroundTag == "Ground" && groundSound != null)
        {
            audioSource.PlayOneShot(groundSound, groundVolume);
        }
        else if (currentGroundTag == "Metal" && metalSound != null)
        {
            audioSource.PlayOneShot(metalSound, metalVolume);
        }
    }

    void CheckGroundMaterial()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.7f))
        {
            if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("Metal"))
                currentGroundTag = hit.collider.tag;
            else
                currentGroundTag = "Untagged";
        }
        else
        {
            currentGroundTag = "Untagged";
        }
    }

    IEnumerator CatchCatSequence()
    {
        if (hasGameEnded) yield break;
        hasGameEnded = true;
        isActing = true;

        if (catGoal != null)
        {
            catGoal.gameObject.SetActive(false);
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (audioSource != null && goalSuccessSound != null)
        {
            audioSource.PlayOneShot(goalSuccessSound, goalSuccessVolume);
            if (clearNarration != null) {
                audioSource.PlayOneShot(clearNarration, clearNarrationVolume);
            }
            yield return new WaitForSeconds(goalSuccessSound.length);
        }

        // ゲーム成功として、スタート画面へ遷移
        SceneManager.LoadScene(startSceneName);
    }
}
