using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace DungeonGen.Generation
{
    /// <summary>
    /// プレイヤー周辺のライトに動的に影を適用してURPの影制限を回避
    /// </summary>
    public class DynamicLightShadowManager : MonoBehaviour
    {
        [Header("Dynamic Shadow Settings")]
        [Tooltip("プレイヤー周辺で影を有効にするライト数")]
        [SerializeField] private int maxShadowLights = 8;

        [Tooltip("影を適用する最大距離")]
        [SerializeField] private float shadowDistance = 20.0f;

        [Tooltip("更新間隔（秒）")]
        [SerializeField] private float updateInterval = 1.0f;

        [Tooltip("プレイヤーの移動距離がこの値を超えたら即座に更新")]
        [SerializeField] private float immediateUpdateDistance = 5.0f;

        [Header("Performance Settings")]
        [Tooltip("初期化時にすべてのライトの影を無効にするか")]
        [SerializeField] private bool disableAllShadowsOnStart = true;

        [Tooltip("シーン内のライトを自動検索するか")]
        [SerializeField] private bool autoFindLights = true;

        [Header("Debug")]
        [Tooltip("デバッグログを表示するか")]
        [SerializeField] private bool enableDebugLogs = false;

        [Tooltip("影付きライトを視覚的に強調表示するか")]
        [SerializeField] private bool visualizeActiveShadowLights = false;

        // 内部変数
        private GameObject currentPlayer;
        private Vector3 lastPlayerPosition;
        private List<Light> allLights = new List<Light>();
        private HashSet<Light> activeShadowLights = new HashSet<Light>();
        private float nextUpdateTime;

        private void Start()
        {
            InitializeSystem();
        }

        private void Update()
        {
            if (currentPlayer == null)
            {
                TryFindPlayer();
                return;
            }

            // プレイヤーが大きく移動した場合は即座に更新
            Vector3 currentPos = currentPlayer.transform.position;
            float movementDistance = Vector3.Distance(currentPos, lastPlayerPosition);

            if (movementDistance >= immediateUpdateDistance || Time.time >= nextUpdateTime)
            {
                UpdateShadowLights();
                lastPlayerPosition = currentPos;
                nextUpdateTime = Time.time + updateInterval;
            }
        }

        /// <summary>
        /// システムの初期化
        /// </summary>
        private void InitializeSystem()
        {
            if (enableDebugLogs)
                Debug.Log("[DynamicLightShadowManager] Initializing dynamic shadow system...");

            TryFindPlayer();

            if (autoFindLights)
            {
                FindAllLights();
            }

            if (disableAllShadowsOnStart)
            {
                DisableAllShadows();
            }

            // 初回更新
            if (currentPlayer != null)
            {
                lastPlayerPosition = currentPlayer.transform.position;
                UpdateShadowLights();
            }
        }

        /// <summary>
        /// プレイヤーを検索
        /// </summary>
        private void TryFindPlayer()
        {
            // DungeonGeneratorから優先取得
            var dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();
            if (dungeonGenerator != null && dungeonGenerator.CurrentPlayer != null)
            {
                currentPlayer = dungeonGenerator.CurrentPlayer;
                if (enableDebugLogs)
                    Debug.Log($"[DynamicLightShadowManager] Found player via DungeonGenerator: {currentPlayer.name}");
                return;
            }

            // Playerタグから検索
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 0)
            {
                currentPlayer = players[0];
                if (enableDebugLogs)
                    Debug.Log($"[DynamicLightShadowManager] Found player via Player tag: {currentPlayer.name}");
                return;
            }

            if (enableDebugLogs)
                Debug.LogWarning("[DynamicLightShadowManager] Player not found! Retrying...");
        }

        /// <summary>
        /// シーン内のすべてのライトを検索
        /// </summary>
        private void FindAllLights()
        {
            allLights.Clear();
            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            foreach (Light light in sceneLights)
            {
                // Point LightとSpot Lightのみを対象
                if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    allLights.Add(light);
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[DynamicLightShadowManager] Found {allLights.Count} eligible lights (Point/Spot only)");
        }

        /// <summary>
        /// すべてのライトの影を無効化
        /// </summary>
        private void DisableAllShadows()
        {
            int disabledCount = 0;

            foreach (Light light in allLights)
            {
                if (light.shadows != LightShadows.None)
                {
                    light.shadows = LightShadows.None;
                    disabledCount++;
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[DynamicLightShadowManager] Disabled shadows on {disabledCount} lights");
        }

        /// <summary>
        /// プレイヤー周辺のライトに影を適用
        /// </summary>
        private void UpdateShadowLights()
        {
            if (currentPlayer == null || allLights.Count == 0)
                return;

            Vector3 playerPos = currentPlayer.transform.position;

            // 距離でソートしてプレイヤーに近いライトを取得
            var nearbyLights = allLights
                .Where(light => light != null && light.gameObject.activeInHierarchy)
                .Select(light => new { Light = light, Distance = Vector3.Distance(playerPos, light.transform.position) })
                .Where(item => item.Distance <= shadowDistance)
                .OrderBy(item => item.Distance)
                .Take(maxShadowLights)
                .Select(item => item.Light)
                .ToList();

            // 以前に影が有効だったライトで、今回は範囲外のライトを無効化
            var lightsToDisable = activeShadowLights.Except(nearbyLights).ToList();
            foreach (Light light in lightsToDisable)
            {
                if (light != null)
                {
                    light.shadows = LightShadows.None;
                    if (enableDebugLogs)
                        Debug.Log($"[DynamicLightShadowManager] Disabled shadow on distant light: {light.name}");
                }
            }

            // 新しく範囲内に入ったライトの影を有効化
            var lightsToEnable = nearbyLights.Except(activeShadowLights).ToList();
            foreach (Light light in lightsToEnable)
            {
                if (light != null)
                {
                    light.shadows = LightShadows.Hard; // ハードシャドウを使用
                    if (enableDebugLogs)
                        Debug.Log($"[DynamicLightShadowManager] Enabled hard shadow on nearby light: {light.name}");
                }
            }

            // アクティブな影ライトのセットを更新
            activeShadowLights.Clear();
            foreach (Light light in nearbyLights)
            {
                activeShadowLights.Add(light);
            }

            if (enableDebugLogs && (lightsToDisable.Count > 0 || lightsToEnable.Count > 0))
            {
                Debug.Log($"[DynamicLightShadowManager] Updated shadows: {activeShadowLights.Count}/{allLights.Count} lights with shadows");
            }
        }

        /// <summary>
        /// 手動でライトリストを更新
        /// </summary>
        [ContextMenu("🔍 Refresh Light List")]
        public void RefreshLightList()
        {
            FindAllLights();
            if (disableAllShadowsOnStart)
            {
                DisableAllShadows();
            }
            UpdateShadowLights();

            Debug.Log($"[DynamicLightShadowManager] Light list refreshed. Found {allLights.Count} lights, {activeShadowLights.Count} with shadows");
        }

        /// <summary>
        /// すべての影を強制無効化
        /// </summary>
        [ContextMenu("❌ Disable All Shadows")]
        public void ForceDisableAllShadows()
        {
            DisableAllShadows();
            activeShadowLights.Clear();
            Debug.Log("[DynamicLightShadowManager] All shadows forcibly disabled");
        }

        /// <summary>
        /// 現在の状態をログ出力
        /// </summary>
        [ContextMenu("📊 Show Status")]
        public void ShowStatus()
        {
            Debug.Log($"[DynamicLightShadowManager] === STATUS ===");
            Debug.Log($"Player: {(currentPlayer ? currentPlayer.name : "Not Found")}");
            Debug.Log($"Total Lights: {allLights.Count}");
            Debug.Log($"Active Shadow Lights: {activeShadowLights.Count}");
            Debug.Log($"Max Shadow Lights: {maxShadowLights}");
            Debug.Log($"Shadow Distance: {shadowDistance}");
            Debug.Log($"Update Interval: {updateInterval}s");
        }

        /// <summary>
        /// 指定されたライトのリストを管理対象に追加
        /// </summary>
        /// <param name="lights">管理対象に追加するライト</param>
        public void AddLights(Light[] lights)
        {
            foreach (Light light in lights)
            {
                if (light != null && !allLights.Contains(light))
                {
                    allLights.Add(light);
                    // 即座に影を無効化
                    light.shadows = LightShadows.None;
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[DynamicLightShadowManager] Added {lights.Length} lights to management");
        }

        /// <summary>
        /// ライトの影を即座に無効化（DungeonGeneratorから呼び出し用）
        /// </summary>
        /// <param name="lightObject">ライトを含むGameObject</param>
        public static void DisableLightShadows(GameObject lightObject)
        {
            if (lightObject == null) return;

            Light[] lights = lightObject.GetComponentsInChildren<Light>();
            foreach (Light light in lights)
            {
                if (light.shadows != LightShadows.None)
                {
                    light.shadows = LightShadows.None;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (currentPlayer == null || !visualizeActiveShadowLights)
                return;

            // プレイヤー周辺の影距離を表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentPlayer.transform.position, shadowDistance);

            // アクティブな影ライトを表示
            Gizmos.color = Color.red;
            foreach (Light light in activeShadowLights)
            {
                if (light != null)
                {
                    Gizmos.DrawWireSphere(light.transform.position, 1.0f);
                    Gizmos.DrawLine(currentPlayer.transform.position, light.transform.position);
                }
            }
        }
    }
}