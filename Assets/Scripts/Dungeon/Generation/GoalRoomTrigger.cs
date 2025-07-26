using UnityEngine;

namespace DungeonGen.Generation
{
    /// <summary>
    /// GoalRoomのトリガー検知用コンポーネント
    /// プレイヤーがGoalRoom専用FloorPrefabのBoxColliderに衝突した時にGameSequenceManagerに通知
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GoalRoomTrigger : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool debugMode = false;

        private BoxCollider triggerCollider;
        private bool hasTriggered = false;

        private void Awake()
        {
            // BoxColliderをトリガーに設定
            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
                Debug.Log($"[GoalRoomTrigger] BoxCollider trigger initialized on '{gameObject.name}' - Size: {triggerCollider.size}, IsTrigger: {triggerCollider.isTrigger}");
            }
            else
            {
                Debug.LogWarning($"[GoalRoomTrigger] No BoxCollider found on '{gameObject.name}'! GoalRoomTrigger requires a BoxCollider.");
            }

            if (debugMode)
            {
                Debug.Log($"[GoalRoomTrigger] Debug mode enabled for: {gameObject.name}");
            }
        }

        private void Start()
        {
            // 初期化後の状態確認
            Debug.Log($"[GoalRoomTrigger] Start() - Position: {transform.position}, Tag: {gameObject.tag}");

            // 周辺のPlayerオブジェクトを確認
            GameObject[] nearbyPlayers = GameObject.FindGameObjectsWithTag(playerTag);
            Debug.Log($"[GoalRoomTrigger] Found {nearbyPlayers.Length} objects with tag '{playerTag}'");

            foreach (GameObject player in nearbyPlayers)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                Debug.Log($"  Player '{player.name}' is {distance:F2} units away");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[GoalRoomTrigger] OnTriggerEnter - Object: '{other.name}', Tag: '{other.tag}', Expected Tag: '{playerTag}'");

            // 既にトリガーされている場合は無視
            if (hasTriggered)
            {
                Debug.Log($"[GoalRoomTrigger] Already triggered, ignoring.");
                return;
            }

            // プレイヤータグをチェック
            if (!other.CompareTag(playerTag))
            {
                Debug.Log($"[GoalRoomTrigger] Tag mismatch. Expected: '{playerTag}', Got: '{other.tag}'");
                return;
            }

            hasTriggered = true;

            Debug.Log($"[GoalRoomTrigger] Player entered goal room: '{other.name}' in goal '{gameObject.name}'");

            // GameSequenceManagerに通知
            NotifyGameSequenceManager(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (debugMode && other.CompareTag(playerTag))
            {
                Debug.Log($"[GoalRoomTrigger] Player staying in goal room: {other.name}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (debugMode && other.CompareTag(playerTag))
            {
                Debug.Log($"[GoalRoomTrigger] Player exited goal room: {other.name}");
            }
        }

        // フォールバック: 通常のCollider接触も検知
        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"[GoalRoomTrigger] OnCollisionEnter - Object: '{collision.gameObject.name}', Tag: '{collision.gameObject.tag}' (This should be trigger, not collision!)");
        }

        /// <summary>
        /// GameSequenceManagerにゴール到達を通知
        /// </summary>
        private void NotifyGameSequenceManager(GameObject player)
        {
            GameSequenceManager gameManager = FindFirstObjectByType<GameSequenceManager>();

            if (gameManager != null)
            {
                gameManager.OnPlayerReachedGoalTrigger(gameObject, player);
            }
            else
            {
                Debug.LogWarning("[GoalRoomTrigger] GameSequenceManager not found! Cannot notify goal reached.");

                // フォールバック: 直接ログ出力
                Debug.Log("[GoalRoomTrigger] Game completed! Player reached the goal.");
                Debug.Log($"[GoalRoomTrigger] Goal position: {transform.position}");
                Debug.Log($"[GoalRoomTrigger] Player: {player.name}");
            }
        }

        /// <summary>
        /// トリガー状態をリセット（ダンジョン再生成時用）
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            if (debugMode)
            {
                Debug.Log($"[GoalRoomTrigger] Trigger reset: {gameObject.name}");
            }
        }

        /// <summary>
        /// デバッグモードの切り替え
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            debugMode = enabled;
        }

        /// <summary>
        /// プレイヤータグの設定
        /// </summary>
        public void SetPlayerTag(string tag)
        {
            playerTag = tag;
        }

        private void OnDrawGizmosSelected()
        {
            // エディタでBoxColliderトリガー範囲を可視化
            if (triggerCollider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(triggerCollider.center, triggerCollider.size);

                // 中心にも小さいキューブを描画
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(triggerCollider.center, triggerCollider.size * 0.1f);
            }
        }
    }
}