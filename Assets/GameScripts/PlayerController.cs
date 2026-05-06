using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 0.2f; // 1マス移動・方向転換にかかる時間
    public float gridSize = 1.0f; // 1マスのサイズ

    [Header("足音設定")]
    public AudioClip groundSound; // 土の足音
    public AudioClip metalSound;  // 鉄板の足音

    [Header("壁衝突設定")]
    public AudioClip bumpSound; // ぶつかった瞬間の音（カンッという音など）
    
    [Header("持続ノイズ設定")]
    [Tooltip("ぶつかった場所に残る持続音（ノイズなど）。未設定の場合は持続音は鳴りません。")]
    public AudioClip sustainNoiseSound; 
    [Tooltip("ノイズ音が持続（再発音）するターン数（0なら持続しない）")]
    public int sustainDurationTurns = 2; 

    [Tooltip("壁にぶつかった時もターンを消費して猫を動かすか")]
    public bool consumeTurnOnBump = true;

    [Header("連携設定")]
    [Tooltip("インスペクターから、対象となる猫(CatGoal)オブジェクトを割り当ててください")]
    public CatGoalController catGoal; // 猫のスクリプトへの参照

    private Rigidbody rb;
    private AudioSource audioSource;
    private Echolocation echolocation; // 反響システムへの参照
    private bool isActing = false; 
    private string currentGroundTag = "Untagged"; 
    
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
        if (isActing) return;

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
        }
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

        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, gridSize))
        {
            // 壁にぶつかった時の処理（音の発生など）を呼び出す
            StartCoroutine(HandleBump(hit.point));

            // 壁にぶつかった行動を「1ターン」として消費するかどうか
            if (consumeTurnOnBump)
            {
                EndTurn(); 
            }

            isActing = false;
            yield break; // 移動はキャンセル
        }

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

        // 移動完了してターン消費
        EndTurn(); 

        isActing = false;
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

        // 方向転換完了してターン消費
        EndTurn(); 

        isActing = false;
    }

    // ★障害物にぶつかった時の音と、その後の持続音を管理するコルーチン
    IEnumerator HandleBump(Vector3 position)
    {
        // ぶつかった位置に、一時的な音源オブジェクトを作成する
        GameObject bumpAudioObj = new GameObject("BumpSound_Temp");
        bumpAudioObj.transform.position = position;
        AudioSource source = bumpAudioObj.AddComponent<AudioSource>();
        source.spatialBlend = 1.0f; // 完全に立体音響にする
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.0f;
        source.maxDistance = 20.0f;

        // 1. まずぶつかった瞬間の音を1回鳴らす
        if (bumpSound != null)
        {
            source.PlayOneShot(bumpSound);
        }

        // 2. 持続音（ノイズ）の設定があり、かつ持続ターン数が1以上の場合
        if (sustainNoiseSound != null && sustainDurationTurns > 0)
        {
            int startTurn = currentTurnCount;
            int lastCheckedTurn = currentTurnCount;

            // 経過ターン数が指定値に達するまで監視を続ける
            while (currentTurnCount < startTurn + sustainDurationTurns)
            {
                // プレイヤーが行動（ターンを消費）するたびに、同じ場所からノイズ音を鳴らす
                if (currentTurnCount > lastCheckedTurn)
                {
                    source.PlayOneShot(sustainNoiseSound);
                    lastCheckedTurn = currentTurnCount;
                }
                yield return null;
            }
        }

        // 最後の音が鳴り終わるまで少し待ってから、一時オブジェクトを削除する
        float waitTime = 1.0f;
        if (sustainNoiseSound != null && sustainDurationTurns > 0) waitTime = sustainNoiseSound.length;
        else if (bumpSound != null) waitTime = bumpSound.length;
        
        yield return new WaitForSeconds(waitTime > 0 ? waitTime : 1.0f);
        Destroy(bumpAudioObj);
    }

    void PlayStepSound()
    {
        if (audioSource == null) return;
        
        bool isSoundPlayed = false;

        if (currentGroundTag == "Ground" && groundSound != null)
        {
            audioSource.PlayOneShot(groundSound);
            isSoundPlayed = true;
        }
        else if (currentGroundTag == "Metal" && metalSound != null)
        {
            audioSource.PlayOneShot(metalSound);
            isSoundPlayed = true;
        }

        if (isSoundPlayed && echolocation != null)
        {
            echolocation.TriggerSonar();
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
}
