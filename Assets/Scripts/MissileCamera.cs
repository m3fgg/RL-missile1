using UnityEngine;

public class MissileCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag your Missile GameObject here.")]
    public Transform target;

    [Header("Camera Positioning")]
    [Tooltip("Position behind (Z) and slightly above (Y) the missile.")]
    public Vector3 offset = new Vector3(0f, 2.5f, -10f);

    [Tooltip("How far ahead of the missile's nose the camera looks.")]
    public float lookAheadDistance = 15f;

    [Header("Smoothing & Horizon")]
    public float positionSmoothness = 15f;
    public float rotationSmoothness = 10f;

    [Tooltip("Keeps the camera upright with the horizon even if the missile barrel-rolls.")]
    public bool lockHorizon = true;

    // LateUpdate runs after all physics and movement for the frame have finished
    void LateUpdate()
    {
        if (target == null) return;

        // 1. Calculate the desired camera position behind the missile
        Vector3 upReference = lockHorizon ? Vector3.up : target.up;
        Quaternion referenceRotation = Quaternion.LookRotation(target.forward, upReference);
        Vector3 desiredPosition = target.position + (referenceRotation * offset);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmoothness * Time.deltaTime
        );

        // 2. Aim the camera slightly ahead of the missile so you can see incoming targets
        Vector3 lookTarget = target.position + (target.forward * lookAheadDistance);
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, upReference);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothness * Time.deltaTime
        );
    }
}