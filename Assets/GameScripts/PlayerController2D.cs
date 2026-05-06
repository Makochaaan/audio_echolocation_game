using System.Collections;
using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController2D : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 0.2f; // 1マス移動・方向転換にかかる時間
    public float gridSize = 1.0f; // 1マスのサイズ
    [Header("足音設定")]
    public AudioClip groundSound; // 土の足音
    public AudioClip metalSound; // 鉄板の足音
    [Header("壁衝突設定")]
    public AudioClip bumpSound; // ぶつかった瞬間の音（カンッという音など）
    [Header("持続ノイズ設定(コンテナ用)")]
    [Tooltip("Containerタグの障害物にぶつかった場所に残る持続音。")]
    public AudioClip sustainNoiseSound;
    [Tooltip("ノイズ音が持続（ループ再生）するターン数（0なら鳴らない）")]
    public int sustainDurationTurns = 2;
    [Tooltip("壁にぶつかった時もターンを消費して猫を動かすか")]
    public bool consumeTurnOnBump = true;
    [Header("立体音響設定 (Resonance Audio)")]
    [Tooltip("Resonance Audio Rendererエフェクトを追加したMixer Groupを割り当ててください")]
    public UnityEngine.Audio.AudioMixerGroup spatialMixerGroup;
    [Header("連携設定")]
    [Tooltip("インスペクターから、対象となる猫(CatGoal)オブジェクトを割り当ててください")]
    public CatGoalController catGoal; // 猫のスクリプトへの参照
    [Header("クリア設定")]
    [Tooltip("猫を捕まえた時に鳴らす音")]
    public AudioClip clearSound;
    [Tooltip("クリア時に表示する文字などのUIオブジェクト（任意）")]
    public GameObject clearUI;
    [Header("操作設定")]
    [Tooltip("スマホのキャリブレーション入力を優先して使う")]
    [SerializeField] private bool useImuInput = true;
    [Tooltip("InterfaceClient への参照（IMU入力）")]
    [SerializeField] private InterfaceClient imuInterface;
    [Tooltip("キャリブレーション入力システム")]
    [SerializeField] private WalkingCalibrationInputSystem walkingCalibration;
    private Rigidbody rb;
    private AudioSource audioSource;
    private AudioSource bumpAudioSource; // 衝突・ノイズ音用の使い回すスピーカー
    private Echolocation echolocation;
    private bool isActing = false;

    private string currentGroundTag = "Untagged";
    private int currentTurnCount = 0;
    private int lastStepCount = 0;
    void Start()
    {
    rb = GetComponent<Rigidbody>();
    rb.isKinematic = true;
    audioSource = GetComponent<AudioSource>();
    audioSource.spatialBlend = 0f;
    echolocation = GetComponent<Echolocation>();
    transform.position = new Vector3(
    Mathf.Round(transform.position.x / gridSize) * gridSize,
    transform.position.y,
    Mathf.Round(transform.position.z / gridSize) * gridSize
    );
    // クリア用のUIがセットされていれば最初は隠しておく
    if (clearUI != null) clearUI.SetActive(false);
    // 衝突音用のスピーカーをあらかじめ1つ作成しておく
    GameObject bumpObj = new GameObject("BumpAndNoiseSource");
    bumpAudioSource = bumpObj.AddComponent<AudioSource>();
    // 指定されたAudioMixerを通す（Resonance Audioエラー回避に必須）
    if (spatialMixerGroup != null)
    {
    bumpAudioSource.outputAudioMixerGroup = spatialMixerGroup;
    }
    // Resonance Audio向けのコンポーネントが存在すれば自動追加して高精度化する
    System.Type resonanceType = System.Type.GetType("ResonanceAudioSource");
    if (resonanceType != null)
    {
    bumpObj.AddComponent(resonanceType);
    }
    // 強力な立体音響(Spatialize)を強制的に有効化
    bumpAudioSource.spatialBlend = 1.0f;
    bumpAudioSource.spatialize = true;
    bumpAudioSource.rolloffMode = AudioRolloffMode.Linear;
    bumpAudioSource.minDistance = 1.0f;
    bumpAudioSource.maxDistance = 20.0f;
    if (imuInterface != null)
    {
    lastStepCount = imuInterface.GetStepCount();
    }
    }
    void Update()
    {
    if (isActing) return;
    // ★追加：クリア判定（プレイヤーと猫が同じ座標にいるか）
    if (catGoal != null && catGoal.gameObject.activeInHierarchy)
    {
    // 距離が非常に近ければ（同じマスに到達していれば）クリア処理へ
    if (Vector3.Distance(transform.position, catGoal.transform.position) < 1.5f)
    {
    StartCoroutine(GameClearRoutine());
    return; // 以降の移動処理は行わない
    }
    }
    if (TryHandleImuInput()) return;
    HandleKeyboardInput();
    }
    bool TryHandleImuInput()
    {
    if (!useImuInput) return false;
    if (!Application.isMobilePlatform) return false;
    if (imuInterface == null || walkingCalibration == null) return false;
    if (!walkingCalibration.IsCalibrated) return false;
    int turnState = imuInterface.GetTurnState();
    if (turnState == 0)
    {
    StartCoroutine(TurnGrid(transform.right));
    return true;
    }
    if (turnState == 1)
    {
    StartCoroutine(TurnGrid(-transform.right));
    return true;
    }
    int currentStepCount = imuInterface.GetStepCount();
    if (currentStepCount > lastStepCount)
    {
    lastStepCount = currentStepCount;
    StartCoroutine(MoveGrid(transform.forward));
    return true;
    }
    return false;
    }
    void HandleKeyboardInput()
    {
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
    // ★追加：ゲームクリア時の演出とシーン遷移
    IEnumerator GameClearRoutine()
    {
    isActing = true; // 以降の操作を完全にロック
    // 猫を捕まえた音（ニャー！やファンファーレ等）を鳴らす
    if (clearSound != null && audioSource != null)
    {
    audioSource.PlayOneShot(clearSound);
    }
    // クリアテキストなどのUIを表示
    if (clearUI != null)
    {
    clearUI.SetActive(true);
    }
    // 3秒間余韻を残して、スタートシーンへ戻る
    yield return new WaitForSeconds(3.0f);
    UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }
    void EndTurn()
    {
    currentTurnCount++;
    if (catGoal != null && catGoal.gameObject.activeInHierarchy)
    {
    catGoal.TakeTurn();
    }
    }
    IEnumerator MoveGrid(Vector3 direction)
    {
    isActing = true;
    Vector3 targetPosition = transform.position + direction * gridSize;
    Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
    if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, gridSize))
    {
    StartCoroutine(HandleBump(hit.point, hit.collider.tag));
    if (consumeTurnOnBump) EndTurn();
    isActing = false;
    yield break;
    }
    CheckGroundMaterial();
    PlayStepSound();
    if (echolocation != null)
    {
    echolocation.TriggerSonar();
    }
    Vector3 startPosition = transform.position;
    float elapsedTime = 0f;
    while (elapsedTime < moveTime)
    {
    transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveTime));
    elapsedTime += Time.deltaTime;
    yield return null;
    }
    transform.position = targetPosition;
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
    EndTurn();
    isActing = false;
    }
    IEnumerator HandleBump(Vector3 position, string hitTag)
    {
    bumpAudioSource.transform.position = position;
    if (bumpSound != null)
    {
    bumpAudioSource.PlayOneShot(bumpSound);
    }
    if (hitTag == "Container" && sustainNoiseSound != null && sustainDurationTurns > 0)
    {
    bumpAudioSource.clip = sustainNoiseSound;
    bumpAudioSource.loop = true;
    bumpAudioSource.Play();
    int targetTurn = currentTurnCount + sustainDurationTurns;
    while (currentTurnCount < targetTurn)
    {
    yield return null;
    }
    bumpAudioSource.Stop();
    }
    else
    {
    if (bumpSound != null) yield return new WaitForSeconds(bumpSound.length);
    }
    }
    void PlayStepSound()
    {
    if (audioSource == null) return;
    if (currentGroundTag == "Ground" && groundSound != null)
    {
    audioSource.PlayOneShot(groundSound);
    }
    else if (currentGroundTag == "Metal" && metalSound != null)
    {
    audioSource.PlayOneShot(metalSound);
    }
    }
    void CheckGroundMaterial()
    {
    Vector3 rayOrigin = transform.position + Vector3.down * 0.4f;
    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.5f))
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
