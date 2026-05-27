using UnityEngine;
using UnityEngine.AI;

public class SmallCockroach : MonoBehaviour
{
    [SerializeField] private Transform[] goals;
    [SerializeField] private bool useRandomMovement = false;
    [SerializeField] private float randomPointSampleRadius = 2f;
    [SerializeField] private int randomPointAttempts = 10;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 4f;
    [SerializeField] private bool stopOnGoal = true;

    public bool hasStartedMoving = true;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform[] instanceGoals;
    private int currentGoalIndex = 0;
    private float idleTimer = 0f;
    private bool isIdling = false;
    private bool isTraversingLink = false;
    private bool movementFinished = false;
    private bool wasStartedMoving = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.updateRotation = false;
        agent.autoTraverseOffMeshLink = false;

        instanceGoals = goals != null ? (Transform[])goals.Clone() : new Transform[0];
        wasStartedMoving = hasStartedMoving;

        if (hasStartedMoving)
        {
            currentGoalIndex = 0;
            movementFinished = !TrySetNextDestination(true);
        }
    }

    void Update()
    {
        if (agent == null) return;

        if (hasStartedMoving != wasStartedMoving)
        {
            wasStartedMoving = hasStartedMoving;
            isIdling = false;
            movementFinished = false;

            if (!hasStartedMoving)
            {
                agent.ResetPath();

                if (animator != null)
                    animator.SetBool("isMoving", false);

                return;
            }

            currentGoalIndex = 0;
            movementFinished = !TrySetNextDestination(true);
        }

        if (!hasStartedMoving || movementFinished) return;

        if (agent.isOnOffMeshLink && !isTraversingLink)
        {
            isTraversingLink = true;
            StartCoroutine(TraverseLink());
            return;
        }

        if (isTraversingLink) return;

        bool isMoving = !agent.pathPending && agent.remainingDistance > agent.stoppingDistance;

        if (animator != null)
            animator.SetBool("isMoving", isMoving);

        HandleRotation();

        if (isMoving || agent.pathPending) return;

        if (!stopOnGoal)
        {
            movementFinished = !TrySetNextDestination();
            return;
        }

        if (!isIdling)
        {
            isIdling = true;
            idleTimer = Random.Range(minIdleTime, maxIdleTime);
            return;
        }

        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            isIdling = false;
            movementFinished = !TrySetNextDestination();
        }
    }

    private System.Collections.IEnumerator TraverseLink()
    {
        OffMeshLinkData linkData = agent.currentOffMeshLinkData;

        Vector3 startPos = transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * agent.baseOffset;

        Vector3 linkDir = (linkData.endPos + Vector3.up * agent.baseOffset)
                        - (linkData.startPos + Vector3.up * agent.baseOffset);
        Quaternion targetRot = linkDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(linkDir.normalized)
            : transform.rotation;

        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Max(distance / agent.speed, 0.001f);
        float elapsed = 0f;

        agent.enabled = false;
        transform.rotation = targetRot;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);

            if (animator != null)
                animator.SetBool("isMoving", true);

            yield return null;
        }

        transform.position = endPos;
        transform.rotation = targetRot;
        agent.enabled = true;
        agent.CompleteOffMeshLink();
        isTraversingLink = false;
    }

    private void HandleRotation()
    {
        if (agent.velocity.sqrMagnitude <= 0.01f) return;
        Vector3 direction = agent.velocity.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private bool TrySetNextDestination(bool isInitialDestination = false)
    {
        if (useRandomMovement)
        {
            if (TryGetRandomNavMeshDestination(out Vector3 destination))
            {
                agent.destination = destination;
                return true;
            }

            return false;
        }

        if (instanceGoals == null || instanceGoals.Length == 0)
            return false;

        if (!isInitialDestination)
        {
            if (currentGoalIndex >= instanceGoals.Length - 1)
                return false;

            currentGoalIndex++;
        }

        agent.destination = instanceGoals[currentGoalIndex].position;
        return true;
    }

    private bool TryGetRandomNavMeshDestination(out Vector3 destination)
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices == null || triangulation.vertices.Length == 0 ||
            triangulation.indices == null || triangulation.indices.Length < 3)
        {
            destination = transform.position;
            return false;
        }

        for (int attempt = 0; attempt < randomPointAttempts; attempt++)
        {
            int triangleIndex = Random.Range(0, triangulation.indices.Length / 3) * 3;

            Vector3 a = triangulation.vertices[triangulation.indices[triangleIndex]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangleIndex + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangleIndex + 2]];

            Vector3 point = GetRandomPointInTriangle(a, b, c);

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, randomPointSampleRadius, NavMesh.AllAreas))
            {
                destination = hit.position;
                return true;
            }
        }

        destination = transform.position;
        return false;
    }

    private Vector3 GetRandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float r1 = Random.value;
        float r2 = Random.value;

        if (r1 + r2 > 1f)
        {
            r1 = 1f - r1;
            r2 = 1f - r2;
        }

        return a + r1 * (b - a) + r2 * (c - a);
    }
}
