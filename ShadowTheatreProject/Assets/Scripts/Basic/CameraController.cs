using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 10, -10);
    
    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private bool lookAtTarget = true;
    
    [Header("Camera Boundaries")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;
    [SerializeField] private float minZ = -10f;
    [SerializeField] private float maxZ = 10f;
    
    [Header("Advanced Settings")]
    [SerializeField] private bool useScreenEdgePanning = false;
    [SerializeField] private float edgePanSpeed = 5f;
    [SerializeField] private float edgeMargin = 20f;
    
    private Vector3 targetPosition;
    
    private void Start()
    {
        // If no target is assigned, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("CameraController: No target assigned and no Player tag found.");
            }
        }
        
        // Set initial position
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
    
    private void LateUpdate()
    {
        if (target == null)
            return;
            
        // Calculate the target position
        targetPosition = target.position + offset;
        
        // Handle screen edge panning if enabled
        if (useScreenEdgePanning)
        {
            ApplyScreenEdgePanning();
        }
        
        // Apply bounds if enabled
        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
        }
        
        // Smooth follow
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime * 10f);
        transform.position = smoothedPosition;
        
        // Make the camera look at the target
        if (lookAtTarget)
        {
            transform.LookAt(target);
        }
    }
    
    private void ApplyScreenEdgePanning()
    {
        Vector3 panDirection = Vector3.zero;
        
        // Check screen edges
        if (Input.mousePosition.x < edgeMargin)
            panDirection.x -= 1;
        else if (Input.mousePosition.x > Screen.width - edgeMargin)
            panDirection.x += 1;
            
        if (Input.mousePosition.y < edgeMargin)
            panDirection.z -= 1;
        else if (Input.mousePosition.y > Screen.height - edgeMargin)
            panDirection.z += 1;
            
        // Apply panning
        if (panDirection != Vector3.zero)
        {
            // Transform direction to world space based on camera orientation
            Vector3 worldDirection = transform.TransformDirection(panDirection.normalized);
            worldDirection.y = 0; // Keep movement on the horizontal plane
            
            // Add to target position
            targetPosition += worldDirection * edgePanSpeed * Time.deltaTime;
        }
    }
    
    // Visual debugging
    private void OnDrawGizmosSelected()
    {
        if (useBounds)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3((minX + maxX) / 2, transform.position.y, (minZ + maxZ) / 2);
            Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
            Gizmos.DrawWireCube(center, size);
        }
        
        if (target != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}