using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController2D : MonoBehaviour
{
    [Header("移動設定")]
    public float moveTime = 1.0f; // 1マス移動・方向転換にかかる時間
    public float gridSize = 1.0f; // 1マスのサイズ
    [Header("足音設定")]
    public AudioClip groundSound; // 土の足音
    public AudioClip metalSound; // 鉄板の足音
    public AudioClip turnSound; // 向き変更時の音
    public AudioClip turnRightSound; // 右回転時の音
    public AudioClip turnLeftSound; // 左回転時の音
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
    [Tooltip("クリア時に鳴らすBGM")]
    public AudioClip clearBgm;
    [Tooltip("クリア時に鳴らすナレーション")]
    public AudioClip clearNarration;
    [Tooltip("クリアBGM用AudioSource（未設定なら通常AudioSourceを使用）")]
    public AudioSource clearBgmSource;
    [Tooltip("クリアナレーション用AudioSource（未設定なら通常AudioSourceを使用）")]
    public AudioSource clearNarrationSource;
    [Tooltip("クリア時に表示する文字などのUIオブジェクト（任意）")]
    public GameObject clearUI;
    [Header("操作設定")]
    [Tooltip("スマホのキャリブレーション入力を優先して使う")]
    [SerializeField] private bool useImuInput = true;
    [Tooltip("IMU入力のデバッグログを出す")]
    [SerializeField] private bool debugImuInput = true;
    [Tooltip("最初の音声が流れている間は移動検知を停止する")]
    [SerializeField] private bool blockInputWhileIntroAudio = true;
    [Tooltip("最初の音声のAudioSource（2Dシーンの導入音声など）")]
    [SerializeField] private AudioSource introAudioSource;
    [Tooltip("InterfaceClient への参照（IMU入力）")]
    [SerializeField] private InterfaceClient imuInterface;
    [Tooltip("キャリブレーション入力システム")]
    [SerializeField] private WalkingCalibrationInputSystem walkingCalibration;
    [Tooltip("IMU移動後、フィードバック音が鳴るまでIMU受付を停止する")]
    [SerializeField] private bool blockImuInputUntilFeedback = true;
    [Tooltip("フィードバック音が鳴らない場合の解除タイムアウト（秒、0以下で無効）")]
    [SerializeField] private float imuFeedbackTimeoutSeconds = 2.0f;
    [Tooltip("IMU歩数の最大蓄積数")]
    [SerializeField] private int maxImuStepAccumulation = 1;
    private Rigidbody rb;
    private AudioSource audioSource;
    private AudioSource bumpAudioSource; // 衝突・ノイズ音用の使い回すスピーカー
    private Echolocation echolocation;
    private bool isActing = false;
    private bool bumpedSinceLastCheck = false;
    private bool inputBlockedUntilRelease = false;
    private bool wasIntroAudioPlaying = false;
    private bool imuInputBlocked = false;
    private Coroutine imuFeedbackTimeoutRoutine;

    private string currentGroundTag = "Untagged";
    private int currentTurnCount = 0;
    private int lastStepCount = 0;
    private int pendingImuSteps = 0;
    void Start()
    {
    rb = GetComponent<Rigidbody>();
    rb.isKinematic = true;
    audioSource = GetComponent<AudioSource>();
    if (clearBgmSource == null)
    {
    clearBgmSource = audioSource;
    }
    if (clearNarrationSource == null)
    {
    clearNarrationSource = audioSource;
    }
    audioSource.spatialBlend = 0f;
    echolocation = GetComponent<Echolocation>();
    if (echolocation != null)
    {
    echolocation.OnEchoFinished += HandleEchoFinished;
    }
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

    if (debugImuInput)
    {
    Debug.Log($"[PlayerController2D] Start: useImuInput={useImuInput}, mobile={Application.isMobilePlatform}, imuInterface={(imuInterface != null)}, walkingCalibration={(walkingCalibration != null)}, persistedCalibration={WalkingCalibrationInputSystem.HasPersistedCalibration}");
    }
    }

    // 破棄時にイベント購読を解除する。
    // Unityのライフサイクルとして呼ばれるよう、必ずクラス直下のメソッドにする
    //（Start内のローカル関数だと呼ばれず、購読が解除されないままになる）
    void OnDestroy()
    {
    if (echolocation != null)
    {
    echolocation.OnEchoFinished -= HandleEchoFinished;
    }
    }

    void Update()
    {
    if (blockInputWhileIntroAudio && introAudioSource != null)
    {
    if (introAudioSource.isPlaying)
    {
    wasIntroAudioPlaying = true;
    return;
    }
    if (wasIntroAudioPlaying)
    {
    wasIntroAudioPlaying = false;
    ResetInputState();
    }
    }
    if (isActing)
    {
    return;
    }
    if (inputBlockedUntilRelease)
    {
    if (!HasKeyboardInput())
    {
    inputBlockedUntilRelease = false;
    }
    else
    {
    return;
    }
    }
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
    if (!Application.isMobilePlatform)
    {
    if (debugImuInput) Debug.Log("[PlayerController2D] IMU skipped: not running on mobile platform.");
    return false;
    }
    if (imuInterface == null)
    {
    if (debugImuInput) Debug.LogWarning("[PlayerController2D] IMU skipped: imuInterface is null.");
    return false;
    }
    if (blockImuInputUntilFeedback && imuInputBlocked)
    {
    if (debugImuInput) Debug.Log("[PlayerController2D] IMU blocked: waiting for feedback audio.");
    return false;
    }
    bool calibrated = (walkingCalibration != null && walkingCalibration.IsCalibrated)
        || WalkingCalibrationInputSystem.HasPersistedCalibration;
    if (!calibrated)
    {
    if (debugImuInput) Debug.Log($"[PlayerController2D] IMU skipped: calibration not ready. walkingCalibration={(walkingCalibration != null)}, IsCalibrated={(walkingCalibration != null && walkingCalibration.IsCalibrated)}, persisted={WalkingCalibrationInputSystem.HasPersistedCalibration}");
    return false;
    }
    int turnState = imuInterface.GetTurnState();
    if (turnState == 0)
    {
    if (debugImuInput) Debug.Log("[PlayerController2D] IMU turn detected: RIGHT");
    StartCoroutine(TurnGrid(transform.right));
    return true;
    }
    if (turnState == 1)
    {
    if (debugImuInput) Debug.Log("[PlayerController2D] IMU turn detected: LEFT");
    StartCoroutine(TurnGrid(-transform.right));
    return true;
    }
    int currentStepCount = imuInterface.GetStepCount();
    if (currentStepCount > lastStepCount)
    {
    int deltaSteps = currentStepCount - lastStepCount;
    pendingImuSteps = Mathf.Min(maxImuStepAccumulation, pendingImuSteps + deltaSteps);
    if (debugImuInput) Debug.Log($"[PlayerController2D] IMU step detected: last={lastStepCount}, current={currentStepCount}, pending={pendingImuSteps}");
    lastStepCount = currentStepCount;
    }
    if (pendingImuSteps > 0)
    {
    pendingImuSteps--;
    StartCoroutine(MoveGrid(transform.forward, true));
    return true;
    }
    if (debugImuInput) Debug.Log($"[PlayerController2D] IMU ready but no action: turnState={turnState}, stepCount={currentStepCount}");
    return false;
    }
    void HandleKeyboardInput()
    {
    if (Keyboard.current == null) return;

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

    if (moveVertical != 0) moveHorizontal = 0;
    if (moveHorizontal != 0 || moveVertical != 0)
    {
    Vector3 inputDirection = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
    float angleDifference = Vector3.Angle(transform.forward, inputDirection);
    if (angleDifference < 1.0f)
    {
    StartCoroutine(MoveGrid(inputDirection, false));
    }
    else
    {
    StartCoroutine(TurnGrid(inputDirection));
    }
    }

    }

    bool HasKeyboardInput()
    {
    if (Keyboard.current == null) return false;
    return Keyboard.current.leftArrowKey.isPressed ||
    Keyboard.current.rightArrowKey.isPressed ||
    Keyboard.current.upArrowKey.isPressed ||
    Keyboard.current.downArrowKey.isPressed ||
    Keyboard.current.aKey.isPressed ||
    Keyboard.current.dKey.isPressed ||
    Keyboard.current.wKey.isPressed ||
    Keyboard.current.sKey.isPressed;
    }
    // ★追加：ゲームクリア時の演出とシーン遷移
    IEnumerator GameClearRoutine()
    {
    isActing = true; // 以降の操作を完全にロック
    // クリアBGMとナレーションを鳴らす
    if (clearBgm != null && clearBgmSource != null)
    {
    clearBgmSource.clip = clearBgm;
    clearBgmSource.loop = false;
    clearBgmSource.Play();
    }
    if (clearNarration != null && clearNarrationSource != null)
    {
    clearNarrationSource.PlayOneShot(clearNarration);
    }
    // クリアテキストなどのUIを表示
    if (clearUI != null)
    {
    clearUI.SetActive(true);
    }
    // BGM終了後に3秒待って待機シーンへ戻る
    float waitTime = (clearBgm != null) ? clearBgm.length : 0f;
    yield return new WaitForSeconds(waitTime + 3.0f);
    UnityEngine.SceneManagement.SceneManager.LoadScene("1. WaitingScene");
    }
    void EndTurn()
    {
    currentTurnCount++;
    if (catGoal != null && catGoal.gameObject.activeInHierarchy)
    {
    catGoal.TakeTurn();
    }
    }
    IEnumerator MoveGrid(Vector3 direction, bool fromImu)
    {
    isActing = true;
    if (fromImu)
    {
    BeginImuFeedbackBlock();
    }
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
    float stepClipLength = PlayStepSound();
    Vector3 startPosition = transform.position;
    float elapsedTime = 0f;
    while (elapsedTime < moveTime)
    {
        transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveTime));
        elapsedTime += Time.deltaTime;
        yield return null;
    }
    transform.position = targetPosition;
    AnalyticsLogger.Event("player_move", new Dictionary<string, object>
    {
        {"fromIMU",fromImu},
        {"x",Mathf.RoundToInt(targetPosition.x)},
        {"z",Mathf.RoundToInt(targetPosition.z)},
    });
    if (stepClipLength > moveTime)
    {
        yield return new WaitForSeconds(stepClipLength - moveTime);
    }
    if (echolocation != null)
    {
        echolocation.TriggerSonar();
    }
    EndTurn();
    isActing = false;
    }
    IEnumerator TurnGrid(Vector3 direction)
    {
    isActing = true;
    float turnDir = Vector3.Dot(direction.normalized, transform.right);
    AnalyticsLogger.Event("player_turn", new Dictionary<string, object>
    {
        {"dir", turnDir > 0.1f ? "right":(turnDir < -0.1f ? "left": "other")}
    });
    if (audioSource != null)
    {
    float turnDot = Vector3.Dot(direction.normalized, transform.right);
    if (turnDot > 0.1f && turnRightSound != null)
    {
    audioSource.PlayOneShot(turnRightSound);
    }
    else if (turnDot < -0.1f && turnLeftSound != null)
    {
    audioSource.PlayOneShot(turnLeftSound);
    }
    else if (turnSound != null)
    {
    audioSource.PlayOneShot(turnSound);
    }
    }
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
    bumpedSinceLastCheck = true;
    AnalyticsLogger.Event("player_bump", new Dictionary<string, object>
    {
        {"tag", hitTag},
    });
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
    NotifyFeedbackEnded();
    }
    else
    {
    if (bumpSound != null) yield return new WaitForSeconds(bumpSound.length);
    NotifyFeedbackEnded();
    }
    }

    public bool ConsumeBumpSignal()
    {
    if (!bumpedSinceLastCheck) return false;
    bumpedSinceLastCheck = false;
    return true;
    }

    public void ResetInputState()
    {
    inputBlockedUntilRelease = true;
    ClearImuFeedbackBlock();
    }

    void BeginImuFeedbackBlock()
    {
    if (!blockImuInputUntilFeedback) return;
    imuInputBlocked = true;
    if (imuFeedbackTimeoutRoutine != null)
    {
    StopCoroutine(imuFeedbackTimeoutRoutine);
    }
    if (imuFeedbackTimeoutSeconds > 0f)
    {
    imuFeedbackTimeoutRoutine = StartCoroutine(ImuFeedbackTimeout());
    }
    }

    IEnumerator ImuFeedbackTimeout()
    {
    yield return new WaitForSeconds(imuFeedbackTimeoutSeconds);
    ClearImuFeedbackBlock();
    }

    void NotifyFeedbackEnded()
    {
    if (!imuInputBlocked) return;
    ClearImuFeedbackBlock();
    }

    void ClearImuFeedbackBlock()
    {
    imuInputBlocked = false;
    if (imuFeedbackTimeoutRoutine != null)
    {
    StopCoroutine(imuFeedbackTimeoutRoutine);
    imuFeedbackTimeoutRoutine = null;
    }
    }

    void HandleEchoFinished()
    {
    NotifyFeedbackEnded();
    }
    float PlayStepSound()
    {
    if (audioSource == null) return 0f;
    if (currentGroundTag == "Ground" && groundSound != null)
    {
    audioSource.PlayOneShot(groundSound);
    return groundSound.length;
    }
    else if (currentGroundTag == "Metal" && metalSound != null)
    {
    audioSource.PlayOneShot(metalSound);
    return metalSound.length;
    }
    else if (groundSound != null)
    {
    // 壁に寄って床タグが取れない場合でも足音を鳴らす
    audioSource.PlayOneShot(groundSound);
    return groundSound.length;
    }
    return 0f;
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
