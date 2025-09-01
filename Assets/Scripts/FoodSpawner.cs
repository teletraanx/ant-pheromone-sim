// FoodSpawner.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class FoodSpawner : MonoBehaviour
{
    [Header("Setup")]
    public FoodPile foodPrefab;            // Prefab must have FoodPile + a trigger collider
    public Transform center;               // Spawn around here; defaults to this transform
    public float spawnRadius = 15f;        // Scatter radius around center
    public int initialCount = 5;           // How many to drop at Start
    public int maxCount = 5;               // Max simultaneous piles

    [Header("Respawn")]
    public float respawnDelay = 6f;        // Delay after a pile is eaten before spawning a new one

    [Header("NavMesh")]
    public float sampleRadius = 3f;        // Search radius for NavMesh.SamplePosition
    public int areaMask = NavMesh.AllAreas;

    [Header("Optional: clear pheromone when pile is eaten")]
    public bool clearOnDepleted = true;    // Requires foodField.ClearArea(...)
    public PheromoneField foodField;       // Your scene FoodField (drag here if clearing)
    public float clearRadius = 2f;         // World meters to wipe around the pile

    private readonly List<FoodPile> active = new();

    void Start()
    {
        if (!center) center = transform;

        int toSpawn = Mathf.Min(initialCount, Mathf.Max(0, maxCount));
        for (int i = 0; i < toSpawn; i++)
            SpawnOne();
    }

    private void SpawnOne()
    {
        if (!foodPrefab)
        {
            Debug.LogWarning("FoodSpawner: assign Food Prefab.");
            return;
        }

        Vector3 origin = center ? center.position : transform.position;

        // Try several random candidates until we find a walkable point
        for (int tries = 0; tries < 24; tries++)
        {
            Vector2 r = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = origin + new Vector3(r.x, 0f, r.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, areaMask))
            {
                // Spawn the pile
                FoodPile pile = Instantiate(foodPrefab, hit.position, Quaternion.identity);

                // Ensure it's tagged and has a trigger collider
                if (pile.gameObject.tag != "Food") pile.gameObject.tag = "Food";
                Collider col = pile.GetComponent<Collider>();
                if (!col) col = pile.gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;

                // Track and subscribe depletion
                active.Add(pile);
                pile.OnDepleted += HandleDepleted;

                return; // success
            }
        }

        Debug.LogWarning("FoodSpawner: failed to find a NavMesh position to spawn on.");
    }

    private void HandleDepleted(FoodPile pile)
    {
        // Optional: wipe pheromone near the eaten pile
        if (clearOnDepleted && foodField)
        {
            // Requires a ClearArea(Vector3 worldPos, float radius) method on your PheromoneField
            foodField.ClearArea(pile.transform.position, clearRadius);
        }

        pile.OnDepleted -= HandleDepleted; // tidy
        active.Remove(pile);

        StartCoroutine(RespawnAfterDelay(respawnDelay));
    }

    private IEnumerator RespawnAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (active.Count < maxCount)
            SpawnOne();
    }
}
