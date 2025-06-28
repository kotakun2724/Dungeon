// WASD + マウスで歩行 & 視点操作する最小構成プレイヤーコントローラ
// ・CharacterController を利用（物理衝突 + 段差処理が楽）
// ・Z/X など個別拡張しやすいようコメントを簡潔に

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;        // m/s
    [SerializeField] float dashSpeed = 10f;       // ダッシュ時の速度
    [SerializeField] float gravity = -9.81f;      // m/s² (Custom Gravity 可)

    [Header("Mouse/Stick Look")]
    [SerializeField] float mouseSensitivity = 2.0f;
    [SerializeField] Transform cameraRoot;        // カメラをぶら下げた Transform

    CharacterController cc;
    float verticalVel;    // Y軸速度
    float pitch;          // カメラ上下角度

    // Input System
    Dungeon input;
    Vector2 moveInput;
    Vector2 lookInput;
    bool isSprinting;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = new Dungeon();
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        // Sprintアクション
        input.Player.Sprint.performed += ctx => isSprinting = true;
        input.Player.Sprint.canceled += ctx => isSprinting = false;
    }

    void OnEnable() { input.Enable(); Cursor.lockState = CursorLockMode.Locked; }
    void OnDisable() { input.Disable(); Cursor.lockState = CursorLockMode.None; }

    void Update()
    {
        Move();
        Look();
    }

    void Move()
    {
        float speed = isSprinting ? dashSpeed : moveSpeed;
        Vector3 moveDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        cc.Move(moveDir * speed * Time.deltaTime);

        // 重力処理
        if (cc.isGrounded && verticalVel < 0)
            verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        cc.Move(Vector3.up * verticalVel * Time.deltaTime);
    }

    void Look()
    {
        // マウス or 右スティック両対応
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);
        pitch = Mathf.Clamp(pitch - mouseY, -80f, 80f);
        if (cameraRoot) cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}

/* 使い方 ---------------------------------------------------------
 * 1. Input System (Dungeon.inputactions) を PlayerInput で有効化
 * 2. Player の子に Main Camera を配置し Transform を cameraRoot に割り当て
 * 3. シーン再生 → WASD/スティックで移動、マウス/右スティックで視点
 * 4. Shift or コントローラーL3押し込みでダッシュ
 * ----------------------------------------------------------------*/
