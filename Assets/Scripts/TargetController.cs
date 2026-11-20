using UnityEngine;

public class TargetController : MonoBehaviour
{
    public Transform envCenter;
    public float sphereRadius = 1000f;

    public float minSpeed = 12f;
    public float maxSpeed = 22f;
    public float turnSpeed = 1.5f;
    public float bankAngle = 45f;
    public float waypointReachDistance = 12f;

    private Rigidbody rb;
    private Vector3 currentWaypoint;
    private float currentSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void Start()
    {
        ResetTarget();
    }

    public void ResetTarget()
    {
        transform.position = GetRandomUpperHemispherePoint(minHeightRatio: 0.1f, maxRadius: sphereRadius * 0.7f);

        Vector3 randomForward = Random.onUnitSphere;
        randomForward.y *= 0.3f;
        if (randomForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(randomForward.normalized, Vector3.up);
        }

        currentSpeed = Random.Range(minSpeed, maxSpeed);
        rb.linearVelocity = transform.forward * currentSpeed;
        rb.angularVelocity = Vector3.zero;

        PickNewWaypoint(innerCoreOnly: false);
    }

    void FixedUpdate()
    {
        float distFromCenter = Vector3.Distance(transform.position, envCenter.position);
        float heightAboveFloor = transform.position.y - envCenter.position.y;
        float floorMargin = sphereRadius * 0.1f;

        bool outOfBounds = distFromCenter > sphereRadius * 0.85f || heightAboveFloor < floorMargin;

        Vector3 waypointOffset = currentWaypoint - envCenter.position;
        bool waypointIsSafe = waypointOffset.magnitude <= sphereRadius * 0.45f && waypointOffset.y >= sphereRadius * 0.2f;

        if (outOfBounds && !waypointIsSafe)
        {
            PickNewWaypoint(innerCoreOnly: true);
        }
        else if (Vector3.Distance(transform.position, currentWaypoint) < waypointReachDistance)
        {
            PickNewWaypoint(innerCoreOnly: false);
        }

        Vector3 toWaypoint = (currentWaypoint - transform.position).normalized;
        Vector3 localDir = transform.InverseTransformDirection(toWaypoint);

        float targetRoll = -Mathf.Clamp(localDir.x, -1f, 1f) * bankAngle;
        Quaternion desiredRotation = Quaternion.LookRotation(toWaypoint, Vector3.up) * Quaternion.Euler(0f, 0f, targetRoll);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime));
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    private void PickNewWaypoint(bool innerCoreOnly)
    {
        float maxRadius = innerCoreOnly ? sphereRadius * 0.35f : sphereRadius * 0.85f;
        float minHeightRatio = innerCoreOnly ? 0.2f : 0.08f;

        currentWaypoint = GetRandomUpperHemispherePoint(minHeightRatio, maxRadius);
        currentSpeed = Random.Range(minSpeed, maxSpeed);
    }

    private Vector3 GetRandomUpperHemispherePoint(float minHeightRatio, float maxRadius)
    {
        Vector3 randomPoint = Random.insideUnitSphere;
        randomPoint.y = Mathf.Abs(randomPoint.y);

        Vector3 offset = randomPoint * maxRadius;
        offset.y = Mathf.Max(offset.y, sphereRadius * minHeightRatio);

        return envCenter.position + offset;
    }
}