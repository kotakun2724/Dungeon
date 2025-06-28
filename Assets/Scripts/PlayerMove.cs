// PlayerMovement.cs – 改良版
// 方向に応じたアニメーション・停止/回転の修正
// 必要に応じて Animator のパラメータ名 "Spead" を変更してください

using UnityEngine;

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
        cam = FindObjectOfType<PlayerCameraController>();
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
        if (capsuleCollider == null) return false;

        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;
        Vector3 bottom = transform.position + capsuleCollider.center - Vector3.up * (height * 0.5f - radius);
        Vector3 checkPos = bottom + moveDirection.normalized * stepSearchDistance;

        // 前方下向きに段差があるかチェック
        RaycastHit hit;
        if (Physics.Raycast(checkPos + Vector3.up * stepHeight, Vector3.down, out hit, stepHeight * 2f, groundLayer))
        {
            float stepHeightFound = hit.point.y - bottom.y;
            return stepHeightFound > 0.1f && stepHeightFound <= stepHeight;
        }
        return false;
    }

    // 段差乗り越え処理
    void StepOver(Vector3 moveDirection)
    {
        if (capsuleCollider == null) return;

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
                // 段差の上に移動
                Vector3 newPos = transform.position;
                newPos.y = Mathf.Lerp(newPos.y, targetY, Time.fixedDeltaTime * 10f);
                transform.position = newPos;
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
        }

        // ===== Animator パラメータ更新 =====
        float targetSpeed = (isRunning ? walkSpeed * runSpeedMultiplier : walkSpeed) * inputDir.magnitude;
        float current = animator.GetFloat(AnimSpeed);
        animator.SetFloat(AnimSpeed, Mathf.Lerp(current, targetSpeed, Time.deltaTime * 10f));
    }

    void FixedUpdate()
    {
        // デバッグ情報
        Debug.Log($"Input: {inputDir}, Velocity: {rb.linearVelocity}");

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
            }
            else
            {
                move = inputDir;
            }

            // ===== 段差乗り越えチェック =====
            if (move.sqrMagnitude > 0.001f && CanStepOver(move))
            {
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

        Debug.Log($"Target: {targetVelocity}, Final: {rb.linearVelocity}");
    }

    #region Ground Check (簡易的)
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
    #endregion
}
