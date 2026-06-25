using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Echolocation : MonoBehaviour
{
    [Header("音の設定")]
    public AudioClip echoSound;  // 反響音
    public AudioClip footstepRSound;  // 右方向の反射音
    public AudioClip footstepLSound;  // 左方向の反射音

    [Header("ソナーの設定")]
    public float maxDistance = 20f; // 音が届く（索敵できる）最大距離
    public float soundSpeed = 10f;  // ゲーム内の音速（小さいほど反響が遅く返ってきます）
    
    [Header("音量減衰の設定")]
    [Tooltip("距離による音量の減少幅をグラフで設定します（横軸が距離、縦軸が音量）")]
    public AnimationCurve volumeRolloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); 

    [Header("立体音響設定 (Resonance Audio)")]
    [Tooltip("Resonance Audio Rendererエフェクトを追加したMixer Groupを割り当ててください")]
    public UnityEngine.Audio.AudioMixerGroup spatialMixerGroup;

    [Header("リバーブ設定")]
    public bool applyReverb = true;
    public AudioReverbPreset reverbPreset = AudioReverbPreset.Cave;
    [Tooltip("PresetがUserの時のみ有効")]
    [Range(-10000f, 0f)]
    public float reverbLevel = -1000f;
    [Tooltip("PresetがUserの時のみ有効")]
    [Range(0.1f, 20f)]
    public float reverbDecayTime = 2.5f;

    // オブジェクトプーリング用：作成したスピーカーを保存しておくリスト
    private List<AudioSource> sourcePool = new List<AudioSource>();

    public event System.Action OnEchoFinished;

    [Header("デバッグ表示")]
    [Tooltip("実機画面に反響音の発生状況をオーバーレイ表示します")]
    public bool showDebug = true;
    [Tooltip("ON にすると spatializer を使わずに鳴らします（実機の無音切り分け用）。鳴れば spatializer が原因")]
    public bool forceDisableSpatialize = false;
    [Tooltip("ON にすると完全2D(spatialBlend=0)で鳴らします。距離減衰を無視するので、鳴れば距離/3D が原因、鳴らなければ出力段が原因")]
    public bool debugForce2D = false;
    private float lastDistToListener = -1f;
    private int sonarCount = 0;   // TriggerSonar が呼ばれた回数
    private int hitCount = 0;     // レイが壁に当たった回数（=エコー予約数）
    private int playCount = 0;    // 実際に source.Play() した回数
    private string lastPlayInfo = "(まだ再生なし)";

    // 空いている（音が鳴り終わった）スピーカーを探す、なければ作る
    private AudioSource GetAvailableSource()
    {
        foreach (var source in sourcePool)
        {
            if (!source.isPlaying) return source; // 空いているスピーカーを再利用
        }

        // 全て使用中なら新しく1つ作成する
        GameObject obj = new GameObject("EchoAudioSource");
        AudioSource newSource = obj.AddComponent<AudioSource>();

        // ★指定されたAudioMixerを通す（Resonance Audioエラー回避に必須）
        if (spatialMixerGroup != null)
        {
            newSource.outputAudioMixerGroup = spatialMixerGroup;
        }

        // Resonance Audio向けのコンポーネントが存在すれば自動追加して高精度化する
        System.Type resonanceType = System.Type.GetType("ResonanceAudioSource");
        if (resonanceType != null)
        {
            obj.AddComponent(resonanceType);
        }
        
        // 強力な立体音響(Spatialize)を強制的に有効化
        newSource.spatialBlend = 1.0f;
        newSource.spatialize = !forceDisableSpatialize;
        
        try 
        {
            newSource.rolloffMode = AudioRolloffMode.Custom; 
            newSource.maxDistance = maxDistance;
            newSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloffCurve);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[立体音響エラー] カーブ設定に失敗しました: {e.Message}");
            newSource.rolloffMode = AudioRolloffMode.Linear; 
            newSource.maxDistance = maxDistance;
        }

        ApplyReverb(newSource);
        sourcePool.Add(newSource);
        return newSource;
    }

    // PlayerController から1歩進むたびに呼び出される
    public void TriggerSonar()
    {
        sonarCount++;
        float[] angles = { 0f, 90f, 180f, 270f };

        foreach (float angle in angles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, maxDistance))
            {
                hitCount++;
                float delay = hit.distance / soundSpeed;
                if (Mathf.Approximately(angle, 90f))
                {
                    StartCoroutine(PlayEchoWith3DSound(hit.point, delay, footstepRSound ?? echoSound));
                }
                else if (Mathf.Approximately(angle, 270f))
                {
                    StartCoroutine(PlayEchoWith3DSound(hit.point, delay, footstepLSound ?? echoSound));
                }
                else
                {
                    StartCoroutine(PlayEchoWith3DSoundPair(hit.point, delay));
                }
                Debug.DrawLine(transform.position, hit.point, Color.red, 1.0f);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * maxDistance, Color.green, 1.0f);
            }
        }
    }

    IEnumerator PlayEchoWith3DSoundPair(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (footstepRSound != null)
        {
            PlayOneShotAtPosition(position, footstepRSound);
        }

        if (footstepLSound != null)
        {
            PlayOneShotAtPosition(position, footstepLSound);
        }

        if (footstepRSound == null && footstepLSound == null && echoSound != null)
        {
            PlayOneShotAtPosition(position, echoSound);
        }
    }

    IEnumerator PlayEchoWith3DSound(Vector3 position, float delay, AudioClip clip)
    {
        yield return new WaitForSeconds(delay);

        if (clip != null)
        {
            PlayOneShotAtPosition(position, clip);
        }
    }

    void PlayOneShotAtPosition(Vector3 position, AudioClip clip)
    {
        AudioSource source = GetAvailableSource();
        // 万が一プールされた音源のSpatializeが外れていた場合は強制的に再適用
        source.spatialize = !forceDisableSpatialize;
        source.spatialBlend = debugForce2D ? 0f : 1f;
        ApplyReverb(source);

        source.transform.position = position;
        source.clip = clip;
        source.Play();

        AudioListener listener = FindObjectOfType<AudioListener>();
        lastDistToListener = (listener != null) ? Vector3.Distance(listener.transform.position, position) : -1f;

        playCount++;
        string mixer = (source.outputAudioMixerGroup != null) ? source.outputAudioMixerGroup.name : "なし(未ルーティング)";
        lastPlayInfo = $"{clip.name} vol={source.volume:0.00} blend={source.spatialBlend:0.0} spat={source.spatialize} playing={source.isPlaying} mixer={mixer}";
        Debug.Log($"[Echo] Play #{playCount} {lastPlayInfo} dist={lastDistToListener:0.0}/max{maxDistance} listener={(listener != null ? listener.name : "なし!!")} pos={position}");

        StartCoroutine(NotifyEchoFinished(source));
    }

    void OnGUI()
    {
        if (!showDebug) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 28;
        style.normal.textColor = Color.green;
        string text =
            "[Echo Debug]\n" +
            $"Sonar呼び出し: {sonarCount}\n" +
            $"壁ヒット(予約): {hitCount}\n" +
            $"再生Play数: {playCount}\n" +
            $"プール数: {sourcePool.Count}\n" +
            $"MixerGroup: {(spatialMixerGroup != null ? spatialMixerGroup.name : "未割当!!")}\n" +
            $"spatialize強制OFF: {forceDisableSpatialize} / 強制2D: {debugForce2D}\n" +
            $"リスナー距離: {lastDistToListener:0.0} / maxDistance: {maxDistance}\n" +
            $"AudioListener.volume: {AudioListener.volume:0.00}\n" +
            $"最後の再生: {lastPlayInfo}";
        GUI.Label(new Rect(20, 20, Screen.width - 40, 500), text, style);
    }

    IEnumerator NotifyEchoFinished(AudioSource source)
    {
        if (source == null || source.clip == null) yield break;

        float clipLength = source.clip.length;
        if (clipLength > 0f)
        {
            yield return new WaitForSeconds(clipLength);
        }

        OnEchoFinished?.Invoke();
    }

    void ApplyReverb(AudioSource source)
    {
        if (!applyReverb || source == null) return;

        AudioReverbFilter filter = source.GetComponent<AudioReverbFilter>();
        if (filter == null)
        {
            filter = source.gameObject.AddComponent<AudioReverbFilter>();
        }

        filter.reverbPreset = reverbPreset;
        if (reverbPreset == AudioReverbPreset.User)
        {
            filter.reverbLevel = reverbLevel;
            filter.decayTime = reverbDecayTime;
        }
    }
}
