using System.Collections.Generic;
using UnityEngine;

public class ObstacleField : MonoBehaviour
{
    [Header("Obstacle Setup")]
    public GameObject obstaclePrefab;
    public int obstacleCount = 8;

    [Header("Spawn Bounds")]
    public Transform missileSpawn;
    public Transform target;
    public float minSpawnRadius = 120f;
    public float maxSpawnRadius = 650f;
    public float safeClearance = 80f;

    private readonly List<Transform> spawnedObstacles = new List<Transform>();

    public void RandomizeObstacles()
    {
        if (obstaclePrefab == null) return;

        SyncPoolSize();

        Vector3 origin = missileSpawn != null ? missileSpawn.position : transform.position;

        for (int i = 0; i < spawnedObstacles.Count; i++)
        {
            Vector3 chosenPos = origin + Vector3.up * minSpawnRadius;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                Vector3 candidate = origin + dir * Random.Range(minSpawnRadius, maxSpawnRadius);

                bool clearOfMissile = Vector3.Distance(candidate, origin) > safeClearance;
                bool clearOfTarget = target == null || Vector3.Distance(candidate, target.position) > safeClearance;

                if (clearOfMissile && clearOfTarget)
                {
                    chosenPos = candidate;
                    break;
                }
            }

            spawnedObstacles[i].position = chosenPos;
            spawnedObstacles[i].rotation = Random.rotation;
        }
    }

    private void SyncPoolSize()
    {
        while (spawnedObstacles.Count < obstacleCount)
        {
            GameObject instance = Instantiate(obstaclePrefab, transform);
            spawnedObstacles.Add(instance.transform);
        }

        while (spawnedObstacles.Count > obstacleCount)
        {
            int last = spawnedObstacles.Count - 1;
            if (spawnedObstacles[last] != null)
            {
                Destroy(spawnedObstacles[last].gameObject);
            }
            spawnedObstacles.RemoveAt(last);
        }
    }
}