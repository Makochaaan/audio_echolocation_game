using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CatGoalController : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 0.3f;     // 1マス移動にかかる時間
    public float gridSize = 0f;     // 1マスのサイズ（プレイヤーと同じにする）

    [Header("音声設定")]
    public AudioClip meowSound;       // 猫が動いた時に発する鳴き声
    [Tooltip("ゲーム開始時にこの候補から1つランダムで meowSound に設定します")]
    public AudioClip[] randomMeowCandidates;

    private AudioSource audioSource;
    private bool isMoving = false;
    [Header("立体音響設定 (Resonance Audio)")]
    [Tooltip("Resonance Audio 用の AudioMixerGroup を割り当てると、3D 空間音響で再生されます。未設定の場合は追加処理を行いません。")]
    public UnityEngine.Audio.AudioMixerGroup spatialMixerGroup;
    // ターン制制御
    [Header("AI設定")]
    [Tooltip("このターン数だけ待機してからプレイヤーへ移動を開始します。")]
    public int idleTurnsBeforeActive = 10;
    [Tooltip("プレイヤーへ移動を試みる間隔（ターン）。2なら2ターンに1回移動します。")]
    public int moveIntervalTurns = 2;
    [Tooltip("プレイヤーのTransform。未設定ならシーン上の Player タグを探します。")]
    public Transform playerTransform;

    private int turnCounter = 0;
    [Header("鳴き声設定")]
    [Tooltip("移動せず静止している間も鳴くかどうか。trueなら idle 期間中に鳴ります。")]
    public bool meowWhileIdle = true;
    [Tooltip("静止時に鳴く間隔（ターン）。1なら毎ターン鳴きます。")]
    public int meowIntervalDuringIdle = 1;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (gridSize <= 0f) gridSize = 1f; // 安全策：0だと割り算でNaNになるため

        // AudioSource を初期化するが、ResonanceAudioSource は
        // 明示的に spatialMixerGroup が割り当てられている場合のみ追加する。
        audioSource.spatialBlend = 1.0f; // 3D
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1.0f;
        audioSource.maxDistance = 20.0f;
        audioSource.playOnAwake = false;

        if (spatialMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = spatialMixerGroup;
            System.Type resonanceType = System.Type.GetType("ResonanceAudioSource");
            if (resonanceType != null && audioSource.GetComponent(resonanceType) == null)
            {
                audioSource.gameObject.AddComponent(resonanceType);
            }
            // 音源の spatialize はミキサーがある場合のみ有効化
            audioSource.spatialize = true;
        }

        // プレイヤー参照が未設定なら Player タグから取得
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // 開始位置をグリッドにピッタリ合わせる
        transform.position = new Vector3(
            Mathf.Round(transform.position.x / gridSize) * gridSize,
            transform.position.y,
            Mathf.Round(transform.position.z / gridSize) * gridSize
        );
    }

    // ★プレイヤーの行動が終わった時に PlayerController から呼ばれるメソッド
    public void TakeTurn()
    {
        // ターン経過をカウント
        turnCounter++;
        PlayMeow(true);

        // 指定ターン数未満は移動しない（ただし鳴き声を出す設定があれば鳴く）
        if (turnCounter < idleTurnsBeforeActive) return;

        // idle期間経過後は moveIntervalTurns 間隔でプレイヤー方向へ移動を試みる
        int sinceActive = turnCounter - idleTurnsBeforeActive;
        if (moveIntervalTurns <= 1 || (sinceActive % moveIntervalTurns) == 0)
        {
            if (!isMoving)
            {
                Vector3 dir = GetDirectionTowardPlayer();
                if (dir != Vector3.zero)
                {
                    Debug.Log($"CatGoalController: Attempting to move towards player (turn={turnCounter}, direction={dir})");
                    StartCoroutine(MoveGrid(dir));
                }
            }
        }
    }

    // 外部から猫の鳴き声を1回鳴らす
    // 通常は移動しているときのみ鳴く。チュートリアル等で強制的に鳴らす場合は force=true を渡す。
    public void PlayMeow(bool force = false)
    {
        if (audioSource == null) return;
        if (!force && !isMoving) return;

        AudioClip clipToPlay = meowSound;

        if (randomMeowCandidates != null && randomMeowCandidates.Length > 0)
        {
            int randomIndex = Random.Range(0, randomMeowCandidates.Length);
            clipToPlay = randomMeowCandidates[randomIndex];
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    // 壁がない移動可能な方向をランダムに取得する
    Vector3 GetRandomValidDirection()
    {
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        List<Vector3> validDirections = new List<Vector3>();

        foreach (Vector3 dir in directions)
        {
            // 進行方向にRayを飛ばして壁判定
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
            if (!Physics.Raycast(rayOrigin, dir, gridSize))
            {
                // 壁がなければ移動可能な方向としてリストに追加
                validDirections.Add(dir);
            }
        }
    // 移動可能な方向が1つ以上あれば、その中からランダムに選ぶ
        if (validDirections.Count > 0)
        {
            int randomIndex = Random.Range(0, validDirections.Count);
            return validDirections[randomIndex];
        }

        return Vector3.zero; // 四方が壁で動けない場合
    }

    // プレイヤーの現在位置を基に、1マス分移動する方向を返す。
    // プレイヤーが同じマスの場合は Vector3.zero を返す。
    Vector3 GetDirectionTowardPlayer()
    {
        if (playerTransform == null) return Vector3.zero;

        Vector3 myPos = new Vector3(
            Mathf.Round(transform.position.x / gridSize) * gridSize,
            transform.position.y,
            Mathf.Round(transform.position.z / gridSize) * gridSize
        );
        Vector3 playerPos = new Vector3(
            Mathf.Round(playerTransform.position.x / gridSize) * gridSize,
            playerTransform.position.y,
            Mathf.Round(playerTransform.position.z / gridSize) * gridSize
        );

        Vector3 delta = playerPos - myPos;
        // 同じ位置なら移動しない
        if (Mathf.Abs(delta.x) < 0.1f && Mathf.Abs(delta.z) < 0.1f) return Vector3.zero;

        // X方向とZ方向の差分が大きい方を優先して1マス進む
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
        {
            return (delta.x > 0) ? Vector3.right : Vector3.left;
        }
        else
        {
            return (delta.z > 0) ? Vector3.forward : Vector3.back;
        }
    }

    // 1マス分移動する処理
    IEnumerator MoveGrid(Vector3 direction)
    {
        isMoving = true;

        // 向かう方向へ顔を向ける
        transform.rotation = Quaternion.LookRotation(direction);

        // ★動く瞬間に鳴き声を再生
        PlayMeow();

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * gridSize;

        float elapsedTime = 0f;

        // スムーズな移動アニメーション
        while (elapsedTime < moveTime)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null; 
        }

        transform.position = targetPosition;
        isMoving = false;
    }
}
