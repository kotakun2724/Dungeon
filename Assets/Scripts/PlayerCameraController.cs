using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    public Transform target; // プレイヤーのTransform
    public float cameraDistance = 5f;
    public Vector3 cameraOffset = new Vector3(0, 2, 0);
    public float mouseSensitivity = 2.0f;
    private float yaw;
    private float pitch;
    private bool isMoveLimited = false;
    private float limitedYaw = 0f;
    public float moveYawLimit = 30f; // 移動中のカメラ後方角度制限（度）

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void SetMoveLimited(bool limited, float targetYaw)
    {
        isMoveLimited = limited;
        limitedYaw = targetYaw;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        yaw += mouseX;
        pitch = Mathf.Clamp(pitch - mouseY, -80f, 80f);

        // 移動中はカメラのyawをプレイヤーの進行方向±30度に制限
        if (isMoveLimited)
        {
            float minYaw = limitedYaw - moveYawLimit;
            float maxYaw = limitedYaw + moveYawLimit;
            yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        }

        if (target)
        {
            Vector3 targetPos = target.position + cameraOffset;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 camPos = targetPos - rot * Vector3.forward * cameraDistance;
            transform.position = camPos;
            transform.LookAt(targetPos);
        }
    }
}