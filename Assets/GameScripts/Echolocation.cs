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

    // オブジェクトプーリング用：作成したスピーカーを保存しておくリスト
    private List<AudioSource> sourcePool = new List<AudioSource>();

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
        newSource.spatialize = true; 
        
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

        sourcePool.Add(newSource);
        return newSource;
    }

    // PlayerController から1歩進むたびに呼び出される
    public void TriggerSonar()
    {
        float[] angles = { 0f, 90f, 180f, 270f };

        foreach (float angle in angles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, maxDistance))
            {
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
        source.spatialize = true;

        source.transform.position = position;
        source.clip = clip;
        source.Play();
    }
}
