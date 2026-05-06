using UnityEngine;

// カメラをターゲットに追従させるコンポーネント
public class CameraController : MonoBehaviour
{
    [Header("追従するターゲット（Player）")]
    public Transform target;
    
    [Header("カメラの位置調整（オフセット）")]
    public Vector3 offset = new Vector3(0f, 6f, -6f);

    // Update の後に呼ばれる LateUpdate でカメラの位置を更新する
    // （プレイヤーの移動が完了した後にカメラを動かすため）
    void LateUpdate()
    {
        if (target != null)
        {
            // カメラの位置を ターゲットの位置 + オフセット に設定
            transform.position = target.position + offset;
            
            // カメラを常にターゲットの方向に向ける
            transform.LookAt(target);
        }
    }
}
