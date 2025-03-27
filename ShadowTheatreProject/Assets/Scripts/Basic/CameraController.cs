using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("摄像机参数")]
    public float mouseSensitivityX = 2f;
    public float mouseSensitivityY = 100.0f;
    public bool invertMouseX = false;
    public bool invertMouseY = false;
    public Vector2 pitchMinMax = new Vector2(-40, 85);
    public float camReturnSpeed = 1f;

    [Header("引用")]
    public CinemachineFreeLook cinemachineCamera;
    public Transform playerTransform;

    private bool isCamReturning;

    void Start()
    {
        // 初始化虚拟相机
        if (cinemachineCamera != null)
        {
            cinemachineCamera.m_YAxis.Value = 0;
            cinemachineCamera.gameObject.SetActive(true);
        }

        // 如果没有指定玩家变换，尝试查找
        if (playerTransform == null)
        {
            PlayerInput playerInput = FindObjectOfType<PlayerInput>();
            if (playerInput != null)
            {
                playerTransform = playerInput.transform;
            }
        }
    }

    void Update()
    {
        if (cinemachineCamera == null) return;

        // 设置鼠标灵敏度
        cinemachineCamera.m_YAxis.m_MaxSpeed = mouseSensitivityY;
        cinemachineCamera.m_XAxis.m_MaxSpeed = mouseSensitivityX * 100f;
        cinemachineCamera.m_YAxis.m_InvertInput = invertMouseY;
        cinemachineCamera.m_XAxis.m_InvertInput = invertMouseX;

        // 鼠标右键回到主视角
        if (Input.GetMouseButtonDown(1))
        {
            isCamReturning = true;
            cinemachineCamera.m_YAxis.m_MaxSpeed = 0;
            cinemachineCamera.m_YAxis.Value = 0;
        }

        if (isCamReturning && playerTransform != null)
        {
            Vector3 cameraForward = GetCameraForwardOnGround();
            if (Vector3.Angle(cameraForward, playerTransform.forward) < 5f)
            {
                isCamReturning = false;
                cinemachineCamera.m_YAxis.m_MaxSpeed = mouseSensitivityY;
                return;
            }

            float rotationDirection = Vector3.Dot(Vector3.Cross(cameraForward, playerTransform.forward), Vector3.up) > 0 ? 1 : -1;
            cinemachineCamera.m_XAxis.Value += camReturnSpeed * rotationDirection * Time.deltaTime * 100;
        }
    }

    // 获取摄像机在地面上的前向投影
    public Vector3 GetCameraForwardOnGround()
    {
        if (cinemachineCamera == null) return Vector3.forward;

        Vector3 cameraForwardWorld = (cinemachineCamera.State.FinalOrientation.normalized * Vector3.forward).normalized;
        Vector3 forwardOnGround = Vector3.ProjectOnPlane(cameraForwardWorld, Vector3.up).normalized;

        return forwardOnGround;
    }
}