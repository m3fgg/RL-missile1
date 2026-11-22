using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class MissileAgentTemp1 : Agent
{
    public Transform target;
    private Rigidbody targetRb;
    private TargetController targetController;

    [Header("Engine & Torque Limits")]
    public float minThrust = 120f;
    public float maxThrust = 400f;
    public float maxPitchYawTorque = 45f;
    public float maxRollTorque = 15f;

    [Header("Aerodynamics")]
    public float forwardDrag = 0.05f;
    public float finSidewaysDrag = 0.8f;
    public float aerodynamicAngularDamping = 1.5f;


    public float maxArenaRadius = 1000f;
    public float initialLaunchSpeed = 25f;
    public float maxWindSpeed = 12f;

    private Rigidbody rb;
    public Transform arena;
    private Vector3 arenaOrigin;
    private Vector3 baseWind;
    private float previousDistance;
    private float initialDistance;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        arenaOrigin = arena.position;
        targetController = target.GetComponent<TargetController>();
        targetRb = target.GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        transform.position = arenaOrigin;
        float randomPitch = Random.Range(-10f, -60f);
        float randomYaw = Random.Range(0, 360f);
        transform.rotation = Quaternion.Euler(randomPitch, randomYaw, 0f);

        rb.linearVelocity = transform.forward * initialLaunchSpeed;
        rb.angularVelocity = Vector3.zero;

        baseWind = new Vector3(
            Random.Range(-maxWindSpeed, maxWindSpeed),
            Random.Range(-maxWindSpeed * 0.3f, maxWindSpeed * 0.3f),
            Random.Range(-maxWindSpeed, maxWindSpeed)
        );


        if (target != null)
        {
            targetController.ResetTarget();
            previousDistance = Vector3.Distance(transform.position, target.position);
            initialDistance = Mathf.Max(previousDistance, 1f);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(GetWindAtPosition(transform.position)));
        sensor.AddObservation(transform.InverseTransformDirection(Physics.gravity.normalized));
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));

            sensor.AddObservation(Mathf.Clamp01(distance / maxArenaRadius));

            sensor.AddObservation(transform.InverseTransformDirection(targetRb.linearVelocity));
        }
        else
        {
            sensor.AddObservation(Vector3.forward);
            sensor.AddObservation(0f);
            sensor.AddObservation(Vector3.zero);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuous = actions.ContinuousActions;

        float pitchAction  = Mathf.Clamp(continuous[0], -1f, 1f);
        float yawAction    = Mathf.Clamp(continuous[1], -1f, 1f);
        float rollAction   = Mathf.Clamp(continuous[2], -1f, 1f);
        float thrustAction = Mathf.Clamp(continuous[3], -1f, 1f);

        float normalizedThrust = (thrustAction + 1f) * 0.5f;
        float thrust = Mathf.Lerp(minThrust, maxThrust, normalizedThrust);
        rb.AddForce(transform.forward * thrust, ForceMode.Force);

        Vector3 localSteerTorque = new Vector3(
            pitchAction * maxPitchYawTorque,
            yawAction * maxPitchYawTorque,
            rollAction * maxRollTorque
        );
        rb.AddRelativeTorque(localSteerTorque, ForceMode.Force);

        rb.AddTorque(-rb.angularVelocity * aerodynamicAngularDamping, ForceMode.Force);

        ApplyAerodynamics(GetWindAtPosition(transform.position));

        EvaluateRewardsAndTermination(pitchAction, yawAction, rollAction);
    }

    private void EvaluateRewardsAndTermination(float pitch, float yaw, float roll)
    {
        if (target == null) return;

        float currentDistance = Vector3.Distance(transform.position, target.position);

        float distanceDelta = previousDistance - currentDistance;
        AddReward((distanceDelta / initialDistance) * 15.0f);
        previousDistance = currentDistance;

        AddReward(-0.008f);

        AddReward(-0.001f * rb.angularVelocity.sqrMagnitude);

        float steeringEffort = (pitch * pitch) + (yaw * yaw) + (roll * roll);
        AddReward(-0.001f * steeringEffort);


        if (transform.position.y < (arenaOrigin.y -2f) || Vector3.Distance(arenaOrigin, transform.position) > maxArenaRadius)
        {
            AddReward(-5.0f);
            EndEpisode();
            return;
        }
    }

    private void ApplyAerodynamics(Vector3 windVelocity)
    {
        Vector3 relativeAirVel = rb.linearVelocity - windVelocity;
        float forwardSpeed = Vector3.Dot(relativeAirVel, transform.forward);

        Vector3 forwardAirVel = transform.forward * forwardSpeed;
        Vector3 sidewaysAirVel = relativeAirVel - forwardAirVel;

        Vector3 noseDrag = -forwardAirVel.normalized * (forwardSpeed * forwardSpeed * forwardDrag);
        Vector3 finDrag = -sidewaysAirVel.normalized * (sidewaysAirVel.sqrMagnitude * finSidewaysDrag);

        rb.AddForce(noseDrag + finDrag, ForceMode.Force);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.name);
        if (other.CompareTag("Target"))
        {
            // HIT
            Debug.Log("HIT!!!");
            float speedBonus = 10.0f * (1f - (float)StepCount / Mathf.Max(MaxStep, 1));
            AddReward(20.0f + speedBonus);
            EndEpisode();
        }
    }

    private Vector3 GetWindAtPosition(Vector3 pos)
    {
        return new Vector3(Mathf.Sin(pos.y * 0.05f) * 10f, 0f, 0f) + baseWind;
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;

        float pitch = 0f;
        if (Input.GetKey(KeyCode.W)) pitch = 1f;
        else if (Input.GetKey(KeyCode.S)) pitch = -1f;
        continuous[0] = pitch;

        float yaw = 0f;
        if (Input.GetKey(KeyCode.D)) yaw = 1f;
        else if (Input.GetKey(KeyCode.A)) yaw = -1f;
        continuous[1] = yaw;

        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll = 1f;
        else if (Input.GetKey(KeyCode.E)) roll = -1f;
        continuous[2] = roll;

        float thrust = 0f;
        if (Input.GetKey(KeyCode.LeftShift)) thrust = 1f;
        else if (Input.GetKey(KeyCode.LeftControl)) thrust = -1f;
        continuous[3] = thrust;
    }
}
