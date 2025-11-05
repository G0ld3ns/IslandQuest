using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;         
    [Header("Offset & Smoothness")]
    public Vector3 offset = new Vector3(0, 10f, -10f);
    public float smoothTime = 0.2f; 
    public bool lookAtTarget = true;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (!target) return;

      
        Vector3 desiredPos = target.position + offset;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);

        if (lookAtTarget)
            transform.LookAt(target);
    }
}
