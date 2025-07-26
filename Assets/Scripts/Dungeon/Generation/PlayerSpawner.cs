using System.Collections.Generic;
using UnityEngine;
using DungeonGen.Core;

namespace DungeonGen.Generation
{
    /// <summary>
    /// ダンジョン内にプレイヤーを生成するクラス
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float spawnHeight = 0.5f; // プレイヤーの生成高さオフセット

        [Header("Spawn Settings")]
        [SerializeField] private bool spawnInFirstRoom = true; // 最初の部屋に生成するか
        [SerializeField] private bool spawnInCenter = true; // 部屋の中央に生成するか
        [SerializeField] private bool preferStartRoom = true; // StartRoomタグがあればそこに優先スポーン

        private GameObject currentPlayer; // 現在生成されているプレイヤー

        /// <summary>
        /// プレイヤーを指定された部屋のリストとマップに基づいて生成
        /// </summary>
        /// <param name="rooms">部屋のリスト</param>
        /// <param name="map">セルマップ</param>
        public void SpawnPlayer(List<RectInt> rooms, CellMap map)
        {
            if (rooms == null || rooms.Count == 0)
            {
                Debug.LogWarning("PlayerSpawner: 部屋が見つかりません。プレイヤーを生成できません。");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogWarning("PlayerSpawner: プレイヤープレハブが設定されていません。");
                return;
            }

            // 既存のプレイヤーがいれば削除
            if (currentPlayer != null)
            {
                DestroyImmediate(currentPlayer);
                currentPlayer = null;
            }

            // 生成位置を決定
            Vector3 spawnPosition = GetSpawnPosition(rooms, map);

            // プレイヤーを生成
            currentPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

            Debug.Log($"プレイヤーを生成しました: {spawnPosition}");
        }

        /// <summary>
        /// 生成位置を計算する
        /// </summary>
        private Vector3 GetSpawnPosition(List<RectInt> rooms, CellMap map)
        {
            // StartRoomタグの床を優先的に探す
            if (preferStartRoom)
            {
                Vector3? startRoomPosition = FindStartRoomPosition();
                if (startRoomPosition.HasValue)
                {
                    Debug.Log("StartRoomタグの床が見つかりました。そこにスポーンします。");
                    return startRoomPosition.Value;
                }
                else
                {
                    Debug.LogWarning("StartRoomタグが見つかりません。通常の部屋選択を使用します。");
                }
            }

            RectInt targetRoom;

            if (spawnInFirstRoom || rooms.Count == 1)
            {
                targetRoom = rooms[0];
            }
            else
            {
                // ランダムな部屋を選択
                int randomIndex = Random.Range(0, rooms.Count);
                targetRoom = rooms[randomIndex];
            }

            Vector2 roomCenter;
            if (spawnInCenter)
            {
                // 部屋の中央
                roomCenter = new Vector2(
                    targetRoom.x + targetRoom.width / 2f,
                    targetRoom.y + targetRoom.height / 2f
                );
            }
            else
            {
                // 部屋内のランダム位置
                roomCenter = new Vector2(
                    Random.Range(targetRoom.x + 1, targetRoom.x + targetRoom.width - 1),
                    Random.Range(targetRoom.y + 1, targetRoom.y + targetRoom.height - 1)
                );
            }

            // 2D座標を3D世界座標に変換
            // Z軸とY軸を入れ替えて、Y軸を高さに使用
            Vector3 worldPosition = new Vector3(
                roomCenter.x,
                spawnHeight,
                roomCenter.y
            );

            return worldPosition;
        }

        /// <summary>
        /// 現在のプレイヤーを取得
        /// </summary>
        public GameObject GetCurrentPlayer()
        {
            return currentPlayer;
        }

        /// <summary>
        /// プレイヤーを削除
        /// </summary>
        public void DestroyPlayer()
        {
            if (currentPlayer != null)
            {
                DestroyImmediate(currentPlayer);
                currentPlayer = null;
                Debug.Log("プレイヤーを削除しました。");
            }
        }

        /// <summary>
        /// StartRoomタグの床を探してその位置を返す
        /// </summary>
        private Vector3? FindStartRoomPosition()
        {
            GameObject[] startRoomObjects = GameObject.FindGameObjectsWithTag("StartRoom");

            if (startRoomObjects.Length > 0)
            {
                // 最初に見つかったStartRoomタグのオブジェクトの位置を使用
                Vector3 startPosition = startRoomObjects[0].transform.position;
                startPosition.y = spawnHeight; // 高さを調整
                return startPosition;
            }

            return null; // StartRoomタグが見つからない
        }

        /// <summary>
        /// 指定された座標が床かどうかチェック
        /// </summary>
        private bool IsValidSpawnLocation(int x, int y, CellMap map)
        {
            return map.InBounds(x, y) && map.IsFloor(x, y);
        }
    }
}