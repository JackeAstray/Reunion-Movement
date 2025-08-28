using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastExample : MonoBehaviour
{
    // 鼠标
    private Mouse mouse;

    // 射线管理器
    private RaycastBase raycastBase;

    public LayerMask layerMask;

    public Camera cameraObj;

    void Start()
    {
        mouse = Mouse.current;

        if (cameraObj == null)
        {
            cameraObj = GetComponent<Camera>();
        }

        raycastBase = new RaycastBase(layerMask, cameraObj);
    }

    void Update()
    {
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            if (raycastBase.CastRayFromScreenPoint(mousePosition, out RaycastHit hitInfo))
            {
                Log.Debug("Hit: " + hitInfo.collider.name);
            }
        }
    }
}
