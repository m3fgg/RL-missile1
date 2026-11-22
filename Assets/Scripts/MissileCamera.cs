using UnityEngine;

public class MissileCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Positioning")]
    public Vector3 offset = new Vector3(0f, 2.5f, -10f);

    public float lookAheadDistance = 15f;

    [Header("Smoothing & Horizon")]
    public float positionSmoothness = 15f;
    public float rotationSmoothness = 10f;

    public bool lockHorizon = true;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 upReference = lockHorizon ? Vector3.up : target.up;
        Quaternion referenceRotation = Quaternion.LookRotation(target.forward, upReference);
        Vector3 desiredPosition = target.position + (referenceRotation * offset);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmoothness * Time.deltaTime
        );

        Vector3 lookTarget = target.position + (target.forward * lookAheadDistance);
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, upReference);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothness * Time.deltaTime
        );
    }
}
