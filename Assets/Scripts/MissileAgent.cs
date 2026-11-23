using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class MissileAgent : Agent
{
    public Transform target;
    private Rigidbody targetRb;
    private TargetController targetController;

    [Header("Engine & Torque Limits")]
    public float minThrust = 500f;
    public float maxThrust = 1500f;
    public float maxPitchYawTorque = 150f;
    public float maxRollTorque = 30f;

    [Header("Aerodynamics")]
    public float forwardDrag = 0.12f;
    public float finSidewaysDrag = 0.9f;
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
    private float closestDistance;
    private bool terminalArmed;


	[Header("Visual Effects")]
    public ParticleSystem exhaustParticles;
    public GameObject explosionPrefab;
    private ParticleSystem.EmissionModule exhaustEmission;
	
	private bool hasHit;

    public ObstacleField obstacleField;
	
	public bool isTrain;
	
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        arenaOrigin = arena.position;
        targetController = target.GetComponent<TargetController>();
        targetRb = target.GetComponent<Rigidbody>();
		
		if (exhaustParticles != null)
		{
			exhaustEmission = exhaustParticles.emission;
		}
    }

    public override void OnEpisodeBegin()
    {
		CancelInvoke(nameof(EndEpisode));
		hasHit = false;
        
        float randomPitch = Random.Range(-10f, -60f);
        float randomYaw = Random.Range(0, 360f);
        Quaternion spawnRot = Quaternion.Euler(randomPitch, randomYaw, 0f);

		RigidbodyInterpolation cachedInterpolation = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;
		
		transform.position = arenaOrigin;
		transform.rotation = spawnRot;
		rb.position = arenaOrigin;
        rb.rotation = spawnRot;
		
		
        rb.linearVelocity = transform.forward * initialLaunchSpeed;
        rb.angularVelocity = Vector3.zero;
		Physics.SyncTransforms();
		rb.interpolation = cachedInterpolation;
		
        baseWind = new Vector3(
            Random.Range(-maxWindSpeed, maxWindSpeed),
            Random.Range(-maxWindSpeed * 0.3f, maxWindSpeed * 0.3f),
            Random.Range(-maxWindSpeed, maxWindSpeed)
        );

        if (target != null)
        {
            targetController.ResetTarget();
            obstacleField.RandomizeObstacles();
            previousDistance = Vector3.Distance(transform.position, target.position);
            initialDistance = Mathf.Max(previousDistance, 1f);
            closestDistance = previousDistance;
            terminalArmed = false;
        }
		
		if (exhaustParticles != null)
		{
			exhaustParticles.Clear();
			exhaustParticles.Play();
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
		if (hasHit) return;
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

        Vector3 toTarget = target.position - transform.position;
        float currentDistance = toTarget.magnitude;

        float distanceDelta = previousDistance - currentDistance;
        AddReward((distanceDelta / initialDistance) * 15.0f);
        previousDistance = currentDistance;

        if (currentDistance < 120f && rb.linearVelocity.sqrMagnitude > 1f)
        {
            float alignment = Vector3.Dot(rb.linearVelocity.normalized, toTarget.normalized);
            AddReward(Mathf.Max(0f, alignment * alignment * alignment) * 0.03f);
        }

        AddReward(-0.008f);
        Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);
        float pitchYawSpin = localAngVel.x * localAngVel.x + localAngVel.y * localAngVel.y;
        float rollSpin = localAngVel.z * localAngVel.z;
        AddReward(-0.001f * pitchYawSpin - 0.015f * rollSpin);

        float steeringEffort = (pitch * pitch) + (yaw * yaw) + (roll * roll);
        AddReward(-0.001f * steeringEffort);

        if (currentDistance <= 35f)
        {
            terminalArmed = true;
            closestDistance = Mathf.Min(closestDistance, currentDistance);
        }

        Vector3 relVel = rb.linearVelocity - (targetRb != null ? targetRb.linearVelocity : Vector3.zero);
        bool flyingAway = Vector3.Dot(toTarget, relVel) < 0f;
        if (terminalArmed && flyingAway && currentDistance > closestDistance + 3f && isTrain)
        {
            AddReward(-5.0f);
            EndEpisode();
            return;
        }

        if (transform.position.y < (arenaOrigin.y - 2f) || Vector3.Distance(arenaOrigin, transform.position) > maxArenaRadius)
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
		if (hasHit) return;
        if (other.CompareTag("Obstacle"))
        {
            AddReward(-5.0f);
            EndEpisode();
            return;
        }
        if (other.CompareTag("Target"))
        {
			hasHit = true;
			if (explosionPrefab != null)
			{
				Vector3 impactPoint = (transform.position + other.transform.position) * 0.5f;
				Instantiate(explosionPrefab, impactPoint, Quaternion.identity);
			}
			Debug.Log("HIT!");
            float speedBonus = 15.0f * (1f - (float)StepCount / Mathf.Max(MaxStep, 1));
            AddReward(25.0f + speedBonus);
            Invoke(nameof(EndEpisode), 0.5f);
        }
    }

    private Vector3 GetWindAtPosition(Vector3 pos)
    {
        return new Vector3(Mathf.Sin(pos.y * 0.05f) * 3f, 0f, 0f) + baseWind;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetKey(KeyCode.W) ? 1f : (Input.GetKey(KeyCode.S) ? -1f : 0f);
        continuous[1] = Input.GetKey(KeyCode.D) ? 1f : (Input.GetKey(KeyCode.A) ? -1f : 0f);
        continuous[2] = Input.GetKey(KeyCode.Q) ? 1f : (Input.GetKey(KeyCode.E) ? -1f : 0f);
        continuous[3] = Input.GetKey(KeyCode.LeftShift) ? 1f : (Input.GetKey(KeyCode.LeftControl) ? -1f : 0f);
    }
}