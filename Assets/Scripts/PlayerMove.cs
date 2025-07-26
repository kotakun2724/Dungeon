// PlayerMovement.cs – 改良版
// 方向に応じたアニメーション・停止/回転の修正
// 必要に応じて Animator のパラメータ名 "Spead" を変更してください

using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeedMultiplier = 2f;
    public float turnSmoothSpeed = 15f;
    public float jumpForce = 5f;
    public float acceleration = 25f;
    public float deceleration = 30f;
    public float maxVelocityChange = 10f;

    [Header("Step Over Settings")]
    public float stepHeight = 0.3f;        // 乗り越えられる段差の高さ
    public float stepSearchDistance = 0.5f; // 段差検出の距離
    public LayerMask groundLayer = 1;      // 地面のレイヤー

    [Header("PlayerMovement Debug Control")]
    [Space(10)]
    [Tooltip("【マスタースイッチ】PlayerMovementの全ログを一括制御")]
    public bool enablePlayerMovementLogs = false;

    [Space(5)]
    [Header("詳細デバッグ設定")]
    [Tooltip("入力とベロシティの情報を表示")]
    public bool showInputVelocityDebug = false;
    [Tooltip("ターゲットと最終ベロシティを表示")]
    public bool showTargetVelocityDebug = false;
    [Tooltip("段差乗り越え情報を表示")]
    public bool showStepOverDebug = false;
    [Tooltip("カメラ連携情報を表示")]
    public bool showCameraDebug = false;
    [Tooltip("グラウンドチェック情報を表示")]
    public bool showGroundCheckDebug = false;

    private Rigidbody rb;
    private Animator animator;
    private PlayerCameraController cam;
    private PhysicsMaterial playerPhysicsMaterial;
    private CapsuleCollider capsuleCollider;

    private Vector3 inputDir;
    private bool isRunning;
    private bool isGrounded;

    // Animator パラメータ (文字列比較コスト削減)
    private static readonly int AnimSpeed = Animator.StringToHash("Spead"); // BlendTree 用
    private static readonly int AnimJump = Animator.StringToHash("Jump");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // より積極的な物理設定
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // マスを軽くして反応を良くする
        rb.mass = 1f;

        // 物理マテリアルを作成・適用（摩擦を完全に除去）
        CreatePlayerPhysicsMaterial();

        animator = GetComponent<Animator>();
        cam = FindFirstObjectByType<PlayerCameraController>();

        // 初期モードの設定とヒント表示
        SetInitialMode();
    }

    void CreatePlayerPhysicsMaterial()
    {
        playerPhysicsMaterial = new PhysicsMaterial("PlayerMaterial");
        playerPhysicsMaterial.dynamicFriction = 0f;
        playerPhysicsMaterial.staticFriction = 0f;
        playerPhysicsMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        playerPhysicsMaterial.bounciness = 0f;
        playerPhysicsMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.material = playerPhysicsMaterial;
        }
    }

    // 段差乗り越え判定
    bool CanStepOver(Vector3 moveDirection)
    {
        if (capsuleCollider == null)
        {
            if (enablePlayerMovementLogs && showStepOverDebug)
            {
                Debug.LogWarning("[PlayerMovement] CanStepOver: CapsuleCollider is null");
            }
            return false;
        }

        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;
        Vector3 bottom = transform.position + capsuleCollider.center - Vector3.up * (height * 0.5f - radius);
        Vector3 checkPos = bottom + moveDirection.normalized * stepSearchDistance;

        // 前方下向きに段差があるかチェック
        RaycastHit hit;
        if (Physics.Raycast(checkPos + Vector3.up * stepHeight, Vector3.down, out hit, stepHeight * 2f, groundLayer))
        {
            float stepHeightFound = hit.point.y - bottom.y;
            bool canStep = stepHeightFound > 0.1f && stepHeightFound <= stepHeight;

            if (enablePlayerMovementLogs && showStepOverDebug)
            {
                Debug.Log($"[PlayerMovement] Step check - Height found: {stepHeightFound:F2}, Can step: {canStep}");
            }

            return canStep;
        }

        if (enablePlayerMovementLogs && showStepOverDebug)
        {
            Debug.Log("[PlayerMovement] No step detected in movement direction");
        }

        return false;
    }

    // 段差乗り越え処理
    void StepOver(Vector3 moveDirection)
    {
        if (capsuleCollider == null)
        {
            if (enablePlayerMovementLogs && showStepOverDebug)
            {
                Debug.LogWarning("[PlayerMovement] StepOver: CapsuleCollider is null");
            }
            return;
        }

        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;
        Vector3 bottom = transform.position + capsuleCollider.center - Vector3.up * (height * 0.5f - radius);
        Vector3 checkPos = bottom + moveDirection.normalized * stepSearchDistance;

        RaycastHit hit;
        if (Physics.Raycast(checkPos + Vector3.up * stepHeight, Vector3.down, out hit, stepHeight * 2f, groundLayer))
        {
            float targetY = hit.point.y + (height * 0.5f - radius);
            if (targetY > transform.position.y)
            {
                Vector3 oldPos = transform.position;

                // 段差の上に移動
                Vector3 newPos = transform.position;
                newPos.y = Mathf.Lerp(newPos.y, targetY, Time.fixedDeltaTime * 10f);
                transform.position = newPos;

                if (enablePlayerMovementLogs && showStepOverDebug)
                {
                    Debug.Log($"[PlayerMovement] Step over executed - From Y: {oldPos.y:F2} to Y: {newPos.y:F2}, Target Y: {targetY:F2}");
                }
            }
        }
    }

    void Update()
    {
        // ===== 入力取得 =====
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputDir = new Vector3(h, 0f, v).normalized;

        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // ===== カメラyaw制限の切り替え =====
        bool isMoving = inputDir.sqrMagnitude > 0.001f;
        if (cam != null)
        {
            float camYaw = cam.transform.eulerAngles.y;
            cam.SetMoveLimited(isMoving, camYaw);
        }

        // ===== ジャンプ =====
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger(AnimJump);

            if (enablePlayerMovementLogs && showInputVelocityDebug)
            {
                Debug.Log($"[PlayerMovement] Jump executed with force: {jumpForce}");
            }
        }

        // ===== Animator パラメータ更新 =====
        float targetSpeed = (isRunning ? walkSpeed * runSpeedMultiplier : walkSpeed) * inputDir.magnitude;
        float current = animator.GetFloat(AnimSpeed);
        float newSpeed = Mathf.Lerp(current, targetSpeed, Time.deltaTime * 10f);
        animator.SetFloat(AnimSpeed, newSpeed);

        if (enablePlayerMovementLogs && showInputVelocityDebug && Mathf.Abs(newSpeed - current) > 0.01f)
        {
            Debug.Log($"[PlayerMovement] Animator speed updated: {current:F2} → {newSpeed:F2} (target: {targetSpeed:F2}, running: {isRunning})");
        }
    }

    void FixedUpdate()
    {
        // デバッグ情報
        if (enablePlayerMovementLogs && showInputVelocityDebug)
        {
            Debug.Log($"[PlayerMovement] Input: {inputDir}, Current Velocity: {rb.linearVelocity}");
        }

        // ===== カメラ基準で方向ベクトルを算出 =====
        Vector3 move = Vector3.zero;
        Vector3 targetVelocity = Vector3.zero;

        if (inputDir.sqrMagnitude > 0.001f)
        {
            if (cam != null)
            {
                Vector3 forward = cam.transform.forward;
                Vector3 right = cam.transform.right;
                forward.y = right.y = 0f;
                forward.Normalize();
                right.Normalize();
                move = forward * inputDir.z + right * inputDir.x;

                if (enablePlayerMovementLogs && showCameraDebug)
                {
                    Debug.Log($"[PlayerMovement] Camera Forward: {forward}, Right: {right}, Final Move: {move}");
                }
            }
            else
            {
                move = inputDir;
                if (enablePlayerMovementLogs && showCameraDebug)
                {
                    Debug.Log($"[PlayerMovement] No camera found, using direct input: {move}");
                }
            }

            // ===== 段差乗り越えチェック =====
            if (move.sqrMagnitude > 0.001f && CanStepOver(move))
            {
                if (enablePlayerMovementLogs && showStepOverDebug)
                {
                    Debug.Log($"[PlayerMovement] Step over detected and executed in direction: {move}");
                }
                StepOver(move);
            }

            // ===== キャラクターを進行方向へ回転（より高速に）=====
            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(move);
                float rotSpeed = turnSmoothSpeed * Time.fixedDeltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed);
            }

            // 目標速度を設定
            float speed = isRunning ? walkSpeed * runSpeedMultiplier : walkSpeed;
            targetVelocity = move * speed;
        }

        // ===== より積極的な速度制御 =====
        Vector3 currentVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velocityDifference = targetVelocity - currentVelocity;

        // 入力がある場合は即座に目標速度に近づける
        if (inputDir.sqrMagnitude > 0.001f)
        {
            // より積極的な加速
            Vector3 velocityChange = Vector3.ClampMagnitude(velocityDifference, maxVelocityChange);
            Vector3 newVelocity = currentVelocity + velocityChange;
            newVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = newVelocity;
        }
        else
        {
            // 停止時は即座に止める
            Vector3 stopVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            stopVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = stopVelocity;
        }

        // 角速度を常にリセット
        rb.angularVelocity = Vector3.zero;

        if (enablePlayerMovementLogs && showTargetVelocityDebug)
        {
            Debug.Log($"[PlayerMovement] Target Velocity: {targetVelocity}, Final Velocity: {rb.linearVelocity}");
        }
    }






    #region Debug Context Menu
    /// <summary>
    /// 現在の状態をコンソールに表示（デバッグ用）
    /// </summary>
    [ContextMenu("📊 Show Current Status")]
    public void ShowCurrentStatus()
    {
        Debug.Log("=== PlayerMovement Current Status ===");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"Rotation: {transform.rotation.eulerAngles}");
        Debug.Log($"Velocity: {rb.linearVelocity}");
        Debug.Log($"Input Direction: {inputDir}");
        Debug.Log($"Is Running: {isRunning}");
        Debug.Log($"Is Grounded: {isGrounded}");
        Debug.Log($"Walk Speed: {walkSpeed}");
        Debug.Log($"Run Speed Multiplier: {runSpeedMultiplier}");
        Debug.Log($"Animator Speed: {animator.GetFloat(AnimSpeed)}");
        Debug.Log($"Camera Reference: {(cam != null ? cam.name : "null")}");
        Debug.Log("=====================================");
    }

    /// <summary>
    /// デバッグ設定を切り替え（デバッグ用）
    /// </summary>
    [ContextMenu("🔄 Toggle PlayerMovement Logs")]
    public void ToggleAllDebugLogs()
    {
        enablePlayerMovementLogs = !enablePlayerMovementLogs;
        Debug.Log($"[PlayerMovement] Debug logs are now: {(enablePlayerMovementLogs ? "ENABLED" : "DISABLED")}");
    }

    /// <summary>
    /// 全てのデバッグログを完全に無効化
    /// </summary>
    [ContextMenu("❌ Disable All PlayerMovement Logs")]
    public void DisableAllDebugLogs()
    {
        enablePlayerMovementLogs = false;
        showInputVelocityDebug = false;
        showTargetVelocityDebug = false;
        showStepOverDebug = false;
        showCameraDebug = false;
        showGroundCheckDebug = false;
        Debug.Log("[PlayerMovement] All debug logs have been DISABLED");
    }

    /// <summary>
    /// デバッグ無効化 + コンソールクリア
    /// </summary>
    [ContextMenu("🚫 Disable PlayerMovement Logs + Clear Console")]
    public void DisableDebugAndClearConsole()
    {
        DisableAllDebugLogs();
#if UNITY_EDITOR
        var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        clearMethod.Invoke(null, null);
#endif
        Debug.Log("🎯 [PlayerMovement] All logs disabled - Game Sequence logs now visible!");
    }

    /// <summary>
    /// ゲームシークエンス表示モード（PlayerMovementログを無効化）
    /// </summary>
    [ContextMenu("🎯 Enable Game Sequence Mode")]
    public void EnableGameSequenceMode()
    {
        enablePlayerMovementLogs = false;
        showInputVelocityDebug = false;
        showTargetVelocityDebug = false;
        showStepOverDebug = false;
        showCameraDebug = false;
        showGroundCheckDebug = false;
#if UNITY_EDITOR
        var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        clearMethod.Invoke(null, null);
#endif
        Debug.Log("🎯 [PlayerMovement] Game Sequence Mode ENABLED - Only game sequence logs will show!");
    }

    /// <summary>
    /// デバッグモード（PlayerMovementログを有効化）
    /// </summary>
    [ContextMenu("🔧 Enable Debug Mode")]
    public void EnableDebugMode()
    {
        enablePlayerMovementLogs = true;
        showInputVelocityDebug = true;
        showTargetVelocityDebug = true;
        Debug.Log("🔧 [PlayerMovement] Debug Mode ENABLED - All PlayerMovement logs are now visible!");
    }

    /// <summary>
    /// Awakeで自動的にGame Sequenceモードに設定
    /// </summary>
    private void SetInitialMode()
    {
        // デフォルトでゲームシークエンスモードに設定
        if (enablePlayerMovementLogs)
        {
            Debug.Log("💡 [PlayerMovement] Use context menu '🎯 Enable Game Sequence Mode' to hide PlayerMovement logs and see game sequence logs clearly!");
        }
    }

    /// <summary>
    /// 現在のデバッグ設定状態を表示
    /// </summary>
    [ContextMenu("⚙️ Show Debug Settings")]
    public void ShowDebugSettings()
    {
        Debug.Log("=== PlayerMovement Debug Settings ===");
        Debug.Log($"Enable PlayerMovement Logs: {enablePlayerMovementLogs}");
        Debug.Log($"Show Input/Velocity Debug: {showInputVelocityDebug}");
        Debug.Log($"Show Target Velocity Debug: {showTargetVelocityDebug}");
        Debug.Log($"Show Step Over Debug: {showStepOverDebug}");
        Debug.Log($"Show Camera Debug: {showCameraDebug}");
        Debug.Log($"Show Ground Check Debug: {showGroundCheckDebug}");
        Debug.Log("======================================");
    }
    #endregion
}
