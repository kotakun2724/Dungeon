using UnityEngine;
using System.Collections;

namespace DungeonGen.Generation
{
    /// <summary>
    /// ゲームシークエンス管理クラス
    /// StartRoomからGoalRoomへの移動を監視し、ゲーム終了を管理
    /// </summary>
    public class GameSequenceManager : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private bool enableGoalDetection = true;
        [SerializeField] private float detectionCheckInterval = 0.5f; // チェック間隔（秒）
        [SerializeField] private bool useTriggerDetection = true; // トリガーベース検知を使用するか

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        private bool gameCompleted = false;
        private DungeonGenerator dungeonGenerator;
        private Coroutine detectionCoroutine;

        private void Start()
        {
            if (verboseLogging)
                Debug.Log("[GameSequenceManager] Starting initialization...");

            // DungeonGeneratorを探す
            dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();

            if (verboseLogging)
            {
                Debug.Log($"[GameSequenceManager] DungeonGenerator found: {dungeonGenerator != null}");
            }

            if (dungeonGenerator == null)
            {
                Debug.LogWarning("[GameSequenceManager] DungeonGeneratorが見つかりません。");
            }

            if (enableGoalDetection)
            {
                StartGoalDetection();
                if (verboseLogging)
                    Debug.Log($"[GameSequenceManager] Goal detection started. Use trigger detection: {useTriggerDetection}");
            }
        }



        private void OnDestroy()
        {
            if (detectionCoroutine != null)
            {
                StopCoroutine(detectionCoroutine);
            }
        }

        /// <summary>
        /// ゴール検知を開始
        /// </summary>
        public void StartGoalDetection()
        {
            if (detectionCoroutine != null)
            {
                StopCoroutine(detectionCoroutine);
            }

            detectionCoroutine = StartCoroutine(GoalDetectionCoroutine());
            Debug.Log("ゴール検知を開始しました。");
        }

        /// <summary>
        /// ゴール検知を停止
        /// </summary>
        public void StopGoalDetection()
        {
            if (detectionCoroutine != null)
            {
                StopCoroutine(detectionCoroutine);
                detectionCoroutine = null;
            }
            Debug.Log("ゴール検知を停止しました。");
        }

        /// <summary>
        /// ゴール検知コルーチン
        /// </summary>
        private IEnumerator GoalDetectionCoroutine()
        {
            while (!gameCompleted && enableGoalDetection)
            {
                CheckPlayerAtGoal();
                yield return new WaitForSeconds(detectionCheckInterval);
            }
        }



        /// <summary>
        /// プレイヤーがゴールにいるかチェック（距離ベース）
        /// </summary>
        private void CheckPlayerAtGoal()
        {
            // トリガーベース検知が有効な場合は距離ベース検知をスキップ
            if (useTriggerDetection)
            {
                if (verboseLogging)
                {
                    Debug.Log("[GameSequence] Using trigger-based detection, skipping distance check.");
                }
                return;
            }

            GameObject player = GetCurrentPlayer();
            if (player == null) return;

            Vector3 playerPosition = player.transform.position;

            // GoalRoomタグのオブジェクトをすべて検索
            GameObject[] goalRoomObjects = GameObject.FindGameObjectsWithTag("GoalRoom");

            foreach (GameObject goalRoom in goalRoomObjects)
            {
                // プレイヤーとゴールルーム床の距離をチェック
                float distance = Vector3.Distance(playerPosition, goalRoom.transform.position);

                // 床のサイズを考慮した判定（セルサイズの半分以内）
                if (distance < 2.3f) // cellSize(4.55f)の約半分
                {
                    OnPlayerReachedGoal(goalRoom);
                    return;
                }
            }

            if (verboseLogging && goalRoomObjects.Length == 0)
            {
                Debug.LogWarning("GoalRoomタグのオブジェクトが見つかりません。");
            }
        }

        /// <summary>
        /// プレイヤーがゴールに到達した時の処理（距離ベース）
        /// </summary>
        private void OnPlayerReachedGoal(GameObject goalRoom)
        {
            if (gameCompleted) return;

            gameCompleted = true;

            Debug.Log("[GameSequenceManager] Game completed! Player reached the goal.");
            Debug.Log($"[GameSequenceManager] Goal position: {goalRoom.transform.position}");

            // ゴール検知を停止
            StopGoalDetection();

            // ここに追加のゲーム終了処理を追加可能
            // 例: シーン切り替え、UI表示、音楽再生など
            OnGameCompleted();
        }

        /// <summary>
        /// プレイヤーがゴールトリガーに到達した時の処理（トリガーベース）
        /// </summary>
        public void OnPlayerReachedGoalTrigger(GameObject goalRoomTrigger, GameObject player)
        {
            if (gameCompleted) return;

            gameCompleted = true;

            Debug.Log("[GameSequenceManager] Game completed! Player reached the goal via trigger.");
            Debug.Log($"[GameSequenceManager] Goal position: {goalRoomTrigger.transform.position}");
            Debug.Log($"[GameSequenceManager] Player: {player.name}");

            // ゴール検知を停止
            StopGoalDetection();

            // ゲーム完了処理を実行
            OnGameCompleted();
        }

        /// <summary>
        /// ゲーム完了時の追加処理
        /// </summary>
        private void OnGameCompleted()
        {
            // 現在はコンソールログのみ
            // 必要に応じてUIの表示、次のシーンへの移行、統計の表示などを追加

            Debug.Log("[GameSequenceManager] Game completion process executed.");

            // プレイヤーの移動を停止（オプション）
            GameObject player = GetCurrentPlayer();
            if (player != null)
            {
                // プレイヤーコントローラーを無効化
                var playerController = player.GetComponent<MonoBehaviour>();
                if (playerController != null)
                {
                    Debug.Log("[GameSequenceManager] Player control disabled.");
                    // playerController.enabled = false; // 必要に応じてコメントアウト
                }
            }
        }

        /// <summary>
        /// ゲームをリセット（デバッグ用）
        /// </summary>
        [ContextMenu("Reset Game")]
        public void ResetGame()
        {
            gameCompleted = false;
            if (enableGoalDetection)
            {
                StartGoalDetection();
            }
            Debug.Log("[GameSequenceManager] Game has been reset.");
        }

        /// <summary>
        /// ゲーム状態の確認（デバッグ用）
        /// </summary>
        [ContextMenu("Check Game Status")]
        public void CheckGameStatus()
        {
            Debug.Log("=== Game Sequence Status ===");
            Debug.Log($"ゲーム完了: {gameCompleted}");
            Debug.Log($"ゴール検知有効: {enableGoalDetection}");
            Debug.Log($"トリガー検知使用: {useTriggerDetection}");

            GameObject[] startRooms = GameObject.FindGameObjectsWithTag("StartRoom");
            GameObject[] goalRooms = GameObject.FindGameObjectsWithTag("GoalRoom");

            Debug.Log($"StartRoomタグのオブジェクト数: {startRooms.Length}");
            Debug.Log($"GoalRoomタグのオブジェクト数: {goalRooms.Length}");

            // GoalRoomトリガーの状態を確認
            int triggersFound = 0;
            foreach (GameObject goalRoom in goalRooms)
            {
                var trigger = goalRoom.GetComponent<GoalRoomTrigger>();
                if (trigger != null)
                {
                    triggersFound++;
                    Debug.Log($"  GoalRoom '{goalRoom.name}' にトリガー設定済み");
                }
                else
                {
                    Debug.LogWarning($"  GoalRoom '{goalRoom.name}' にトリガーなし");
                }
            }
            Debug.Log($"GoalRoomトリガー設定数: {triggersFound}/{goalRooms.Length}");

            GameObject player = GetCurrentPlayer();
            Debug.Log($"プレイヤー存在: {player != null}");
            if (player != null)
            {
                Debug.Log($"プレイヤー位置: {player.transform.position}");
                Debug.Log($"プレイヤータグ: {player.tag}");
            }
            Debug.Log("==============================");
        }

        /// <summary>
        /// トリガー状態をリセット（デバッグ用）
        /// </summary>
        [ContextMenu("Reset All Goal Triggers")]
        public void ResetAllGoalTriggers()
        {
            GameObject[] goalRooms = GameObject.FindGameObjectsWithTag("GoalRoom");
            int resetCount = 0;

            foreach (GameObject goalRoom in goalRooms)
            {
                var trigger = goalRoom.GetComponent<GoalRoomTrigger>();
                if (trigger != null)
                {
                    trigger.ResetTrigger();
                    resetCount++;
                }
            }

            Debug.Log($"Goal triggers reset: {resetCount}/{goalRooms.Length}");
        }

        /// <summary>
        /// 強制的にゴール達成をテスト（デバッグ用）
        /// </summary>
        [ContextMenu("Test Goal Trigger")]
        public void TestGoalTrigger()
        {
            GameObject player = GetCurrentPlayer();
            if (player == null)
            {
                Debug.LogWarning("[GameSequenceManager] No player found for test.");
                return;
            }

            GameObject[] goalRooms = GameObject.FindGameObjectsWithTag("GoalRoom");
            if (goalRooms.Length == 0)
            {
                Debug.LogWarning("[GameSequenceManager] No GoalRoom found for test.");
                return;
            }

            Debug.Log($"[GameSequenceManager] Testing goal trigger with player: {player.name}");
            OnPlayerReachedGoalTrigger(goalRooms[0], player);
        }



        /// <summary>
        /// ゴール検知の状態診断（デバッグ用）
        /// </summary>
        [ContextMenu("Show Diagnostic")]
        public void ShowDiagnostic()
        {
            Debug.Log("[GameSequenceManager] === DIAGNOSTIC ===");
            Debug.Log($"GameSequenceManager Active: {gameObject.activeInHierarchy}");
            Debug.Log($"Enable Goal Detection: {enableGoalDetection}");
            Debug.Log($"Use Trigger Detection: {useTriggerDetection}");
            Debug.Log($"Game Completed: {gameCompleted}");

            CheckTagObjects("Player");
            CheckTagObjects("GoalRoom");
        }

        /// <summary>
        /// 強制的にプレイヤーをGoalRoomの中心に配置（テスト用）
        /// </summary>
        [ContextMenu("Move Player to Goal")]
        public void MovePlayerToGoal()
        {
            GameObject player = GetCurrentPlayer();
            GameObject[] goalRooms = GameObject.FindGameObjectsWithTag("GoalRoom");

            if (player == null)
            {
                Debug.LogWarning("[GameSequenceManager] No player found for movement.");
                return;
            }

            if (goalRooms.Length == 0)
            {
                Debug.LogWarning("[GameSequenceManager] No GoalRoom found for movement.");
                return;
            }

            // GoalRoomの中心に配置
            Vector3 goalCenter = goalRooms[0].transform.position;
            goalCenter.y += 2.0f; // 少し高めに配置

            player.transform.position = goalCenter;

            Debug.Log($"[GameSequenceManager] Moved player to goal center: {goalCenter}");
        }

        /// <summary>
        /// Playerタグが存在するかチェック
        /// </summary>
        private void CheckPlayerTagExists()
        {
            // 各種タグの存在確認
            CheckTagObjects("Player");
            CheckTagObjects("StartRoom");
            CheckTagObjects("GoalRoom");
        }

        /// <summary>
        /// 指定されたタグのオブジェクトを確認
        /// </summary>
        private void CheckTagObjects(string tagName)
        {
            if (!verboseLogging) return;

            try
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tagName);
                Debug.Log($"[GameSequenceManager] Found {taggedObjects.Length} objects with '{tagName}' tag");

                for (int i = 0; i < taggedObjects.Length; i++)
                {
                    GameObject obj = taggedObjects[i];
                    Debug.Log($"  {tagName} {i + 1}: {obj.name} at position {obj.transform.position}");

                    if (tagName == "Player")
                    {
                        // Colliderの確認
                        Collider[] colliders = obj.GetComponents<Collider>();
                        Debug.Log($"    Colliders: {colliders.Length}");

                        for (int j = 0; j < colliders.Length; j++)
                        {
                            Debug.Log($"      Collider {j + 1}: {colliders[j].GetType().Name}, IsTrigger: {colliders[j].isTrigger}");
                        }
                    }
                    else if (tagName == "GoalRoom")
                    {
                        // GoalFloor Prefabかどうかの確認
                        bool isGoalFloorPrefab = obj.name.StartsWith("GoalFloor_");
                        Debug.Log($"    Is GoalFloor Prefab: {isGoalFloorPrefab}");

                        // GoalRoomトリガーの確認
                        var trigger = obj.GetComponent<GoalRoomTrigger>();
                        var boxCollider = obj.GetComponent<BoxCollider>();
                        Debug.Log($"    GoalRoomTrigger: {trigger != null}");
                        Debug.Log($"    BoxCollider: {(boxCollider != null ? $"Size: {boxCollider.size}, IsTrigger: {boxCollider.isTrigger}" : "None")}");

                        if (isGoalFloorPrefab && trigger != null)
                        {
                            Debug.Log($"    Status: Ready for goal detection");
                        }
                        else if (isGoalFloorPrefab && trigger == null)
                        {
                            Debug.Log($"    Status: GoalFloor prefab but missing trigger component");
                        }
                        else if (!isGoalFloorPrefab)
                        {
                            Debug.Log($"    Status: Not a GoalFloor prefab (will not trigger goal)");
                        }
                    }
                }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"[GameSequenceManager] Tag '{tagName}' does not exist in Tag Manager! Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のプレイヤーを取得（DungeonGeneratorから）
        /// </summary>
        private GameObject GetCurrentPlayer()
        {
            // DungeonGeneratorから取得を優先
            if (dungeonGenerator != null)
            {
                GameObject player = dungeonGenerator.CurrentPlayer;
                if (player != null) return player;
            }

            // フォールバック: Playerタグで検索
            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (playerObjects.Length > 0)
            {
                return playerObjects[0];
            }

            return null;
        }
    }
}