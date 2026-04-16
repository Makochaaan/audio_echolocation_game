using System.Collections;
using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class Echolocation : MonoBehaviour
{
    [Header("音の設定")]
    public AudioClip echoSound;  // 反響音
    [Range(0f, 1f)] public float echoVolume = 1f;


    [Header("ソナーの設定")]
    public float maxDistance = 20f; // 音が届く（索敵できる）最大距離
    public float soundSpeed = 10f;  // ゲーム内の音速（小さいほど反響が遅く返ってきます）
    
    [Header("音量減衰の設定")]
    [Tooltip("距離による音量の減少幅をグラフで設定します（横軸が距離、縦軸が音量）")]
    public AnimationCurve volumeRolloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); 


    // PlayerController から1歩進むたびに呼び出される
    public void TriggerSonar()
    {
        TriggerSonarWithClip(echoSound, echoVolume);
    }


    public void TriggerSonarWithClip(AudioClip sonarClip, float volumeScale = 1f)
    {
        float[] angles = { 0f,90f, 180f, 270f};


        foreach (float angle in angles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;


            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, maxDistance))
            {
                float delay = hit.distance / soundSpeed;
                StartCoroutine(PlayEchoWith3DSound(hit.point, delay, sonarClip, Mathf.Clamp01(volumeScale)));
                Debug.DrawLine(transform.position, hit.point, Color.red, 1.0f);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * maxDistance, Color.green, 1.0f);
            }
        }
    }


    IEnumerator PlayEchoWith3DSound(Vector3 position, float delay, AudioClip clip, float volumeScale)
    {
        yield return new WaitForSeconds(delay);


        if (clip != null)
        {
            GameObject tempAudioObj = new GameObject("TempEchoAudio");
            tempAudioObj.transform.position = position;


            AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
            tempSource.clip = clip;
            tempSource.spatialBlend = 1.0f; 
            tempSource.volume = volumeScale;
            
            try 
            {
                // カーブの設定（もしここでエラーが起きても、上のDestroy予約によりオブジェクトは確実に消えます）
                tempSource.rolloffMode = AudioRolloffMode.Custom; 
                tempSource.maxDistance = maxDistance;
                tempSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloffCurve);
            }
            catch
            {
                // エラー時は安全な標準モードに切り替える
                tempSource.rolloffMode = AudioRolloffMode.Linear; 
                tempSource.maxDistance = maxDistance;
            }
            
            tempSource.Play();
            StartCoroutine(DestroyAfterPlayback(tempAudioObj, tempSource));
        }
    }


    IEnumerator DestroyAfterPlayback(GameObject audioObject, AudioSource audioSource)
    {
        if (audioObject == null || audioSource == null)
        {
            yield break;
        }


        while (audioSource.isPlaying)
        {
            yield return null;
        }


        Destroy(audioObject);
    }
}
