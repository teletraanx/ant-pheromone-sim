using UnityEngine;
using UnityEngine.AI;

public class ant : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float wanderTimer = 3f;

    private NavMeshAgent agent;
    private float timer;

    // Pheromones
    public PheromoneField homeField;   // drag your Home field here
    public PheromoneField foodField;   // drag your Food field here

    [Header("Pheromone Params")]
    public float depositRate = 5f;     // units/sec (only while carrying)

    [Header("Sniffing")]
    public float sensorDistance = 0.8f;
    public float sensorAngle = 30f;
    public float lookahead = 3f;       // meters toward the winning direction
    public float scentThreshold = 0.01f;

    [Header("Sniff cadence")]
    public float sniffInterval = 0.3f;
    private float sniffTimer = 0f;

    public Transform nest;
    public bool carrying = false;

    private void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;
        PickNewDestination();
    }

    void Update()
    {
        // periodic scent check to opportunistically nudge destination
        sniffTimer += Time.deltaTime;
        if (sniffTimer >= sniffInterval)
        {
            var sniff = carrying ? homeField : foodField;
            var goal = GetScentBiasedGoal(sniff);
            if (goal.HasValue) agent.SetDestination(goal.Value);
            sniffTimer = 0f;
        }

        // wander cycle or path complete → pick a new destination
        timer += Time.deltaTime;
        bool reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

        if (timer >= wanderTimer || reached)
        {
            PickNewDestination();
            timer = 0f;
        }

        // drop breadcrumbs only while carrying
        if (carrying && foodField != null)
            foodField.Deposit(transform.position, depositRate * Time.deltaTime);
    }

    private void PickNewDestination()
    {
        // 1) try following the right scent (Food when searching, Home when carrying)
        var sniff = carrying ? homeField : foodField;
        Vector3? scentGoal = GetScentBiasedGoal(sniff);
        if (scentGoal.HasValue)
        {
            agent.SetDestination(scentGoal.Value);
            return;
        }

        // 2) if carrying but no gradient, head to nest as a fallback
        if (carrying && nest != null)
        {
            agent.SetDestination(nest.position);
            return;
        }

        // 3) otherwise, random wander on the NavMesh
        Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas);
        agent.SetDestination(newPos);
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int areaMask)
    {
        Vector2 r = Random.insideUnitCircle * dist;
        Vector3 candidate = new Vector3(origin.x + r.x, origin.y, origin.z + r.y);

        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, dist, areaMask)
            ? hit.position
            : origin;
    }

    private Vector3? GetScentBiasedGoal(PheromoneField field)
    {
        if (field == null) return null;

        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 left = Quaternion.Euler(0f, +sensorAngle, 0f) * fwd;
        Vector3 right = Quaternion.Euler(0f, -sensorAngle, 0f) * fwd;

        float sF = field.Sample(pos + fwd * sensorDistance);
        float sL = field.Sample(pos + left * sensorDistance);
        float sR = field.Sample(pos + right * sensorDistance);

        float max = Mathf.Max(sF, Mathf.Max(sL, sR));
        if (max < scentThreshold) return null;

        // require the winner to beat the runner-up by a small margin
        float mid = (sF + sL + sR - max) - Mathf.Min(sF, Mathf.Min(sL, sR));
        if (max - mid < 0.005f) return null;

        Vector3 dir = (max == sL) ? left : (max == sR) ? right : fwd;
        Vector3 goal = pos + dir * lookahead;

        return NavMesh.SamplePosition(goal, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : (Vector3?)null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!carrying && other.CompareTag("Food"))
        {
            var pile = other.GetComponent<FoodPile>();
            if (pile != null && pile.Take(1))
                carrying = true; // picked up food → start depositing + bias to home
        }
        else if (carrying && other.CompareTag("Nest"))
        {
            carrying = false;   // delivered at nest
        }
    }
}
