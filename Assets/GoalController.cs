using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CatGoalController : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 0.3f;     // 1マス移動にかかる時間
    public float gridSize = 1.0f;     // 1マスのサイズ（プレイヤーと同じにする）

    [Header("音声設定")]
    public AudioClip meowSound;       // 猫が動いた時に発する鳴き声
    [Tooltip("ゲーム開始時にこの候補から1つランダムで meowSound に設定します")]
    public AudioClip[] randomMeowCandidates;

    private AudioSource audioSource;
    private bool isMoving = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
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
        if (!isMoving)
        {
            // 移動可能な方向をランダムに取得
            Vector3 moveDirection = GetRandomValidDirection();
            
            if (moveDirection != Vector3.zero)
            {
                StartCoroutine(MoveGrid(moveDirection));
            }
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

    // 1マス分移動する処理
    IEnumerator MoveGrid(Vector3 direction)
    {
        isMoving = true;

        // 向かう方向へ顔を向ける
        transform.rotation = Quaternion.LookRotation(direction);

        // ★動く瞬間に鳴き声をランダム選択してワンショット再生する
        if (audioSource != null)
        {
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
