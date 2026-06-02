using UnityEngine;
using UnityEngine.AI;

public class SmallCockroach : MonoBehaviour
{

    //cockroach type enum
    public enum CockroachType
    {
        Small,
        Large,
        Other
    }

    [SerializeField] private Transform[] goals;
    [SerializeField] private bool useRandomMovement = false;
    [SerializeField] private bool useRandomScale = false;
    [SerializeField] private float randomPointSampleRadius = 0.35f;
    [SerializeField] private int randomPointAttempts = 10;
    [SerializeField] private float minRandomMoveRadius = 0.2f;
    [SerializeField] private float maxRandomMoveRadius = 0.75f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 4f;
    [SerializeField] private float stuckTimeout = 0.75f;
    [SerializeField] private float stuckVelocityThreshold = 0.02f;
    [SerializeField] private bool stopOnGoal = true;
    [SerializeField] private bool ignoreCockroaches = false;
    [SerializeField] private CockroachType cockroachType = CockroachType.Small;
    [SerializeField] [Range(0f, 1f)] private float crossLinkBias = 0.6f;
    [SerializeField] private float largeTypeWallMargin = 0.15f;
    [SerializeField] private float largeBlockCheckRadius = 0.4f;
    [SerializeField] private float largeBlockCheckDistance = 0.6f;
    [SerializeField] private float minRandomMoveDuration = 1.5f;
    [SerializeField] private float maxRandomMoveDuration = 4f;

    [SerializeField] private Animator animatorOverride;
    public bool hasStartedMoving = true;

    public float MinRandomScale = 0.5f;
    public float MaxRandomScale = 1.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform[] instanceGoals;
    private int currentGoalIndex = 0;
    private float idleTimer = 0f;
    private float stuckTimer = 0f;
    private float baseAgentRadius;
    private float baseAgentHeight;
    private bool isIdling = false;
    private bool isTraversingLink = false;
    private bool movementFinished = false;
    private bool wasStartedMoving = false;
    private float normalAgentRadius;
    private float randomMoveTimer = 0f;


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = animatorOverride != null ? animatorOverride : GetComponent<Animator>();

        baseAgentRadius = agent.radius;
        baseAgentHeight = agent.height;

        agent.updateRotation = false;
        agent.autoTraverseOffMeshLink = false;
        agent.autoRepath = true;

        ApplyRandomScale();
        ConfigureAgentAvoidance();
        ApplyWallMargin();

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
            stuckTimer = 0f;

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

        // Tick the random move duration timer and stop early when it expires.
        if (useRandomMovement && randomMoveTimer > 0f)
        {
            randomMoveTimer -= Time.deltaTime;
            if (randomMoveTimer <= 0f)
            {
                randomMoveTimer = 0f;
                stuckTimer = 0f;
                agent.ResetPath();

                // Enter idle so the cockroach pauses before picking a new goal.
                isIdling = true;
                idleTimer = Random.Range(minIdleTime, maxIdleTime);
                return;
            }
        }

        bool wantsToMove = !agent.pathPending && agent.hasPath && agent.remainingDistance > agent.stoppingDistance;
        bool isMoving = wantsToMove && agent.velocity.sqrMagnitude > stuckVelocityThreshold * stuckVelocityThreshold;

        UpdateStuckTimer(wantsToMove, isMoving);

        if (stuckTimer >= stuckTimeout)
        {
            stuckTimer = 0f;
            isIdling = false;
            agent.ResetPath();
            movementFinished = !TrySetNextDestination();
            return;
        }

        if (cockroachType == CockroachType.Large && IsPathBlockedByCockroach())
        {
            stuckTimer = 0f;
            isIdling = false;
            agent.ResetPath();
            movementFinished = !TrySetNextDestination();
            return;
        }

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

    private void ApplyRandomScale()
    {
        if (!useRandomScale)
        {
            normalAgentRadius = agent.radius;
            return;
        }

        float scaleMultiplier = Random.Range(MinRandomScale, MaxRandomScale);
        transform.localScale *= scaleMultiplier;
        agent.radius = Mathf.Max(0.01f, baseAgentRadius * scaleMultiplier);
        agent.height = Mathf.Max(agent.radius, baseAgentHeight * scaleMultiplier);
        normalAgentRadius = agent.radius;
    }

    private void ConfigureAgentAvoidance()
    {
        if (ignoreCockroaches)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.avoidancePriority = 0;
        }
        else
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            switch (cockroachType)
            {
                case CockroachType.Small:  agent.avoidancePriority = Random.Range(10, 30); break;
                case CockroachType.Large:  agent.avoidancePriority = Random.Range(40, 60); break;
                case CockroachType.Other:  agent.avoidancePriority = Random.Range(70, 90); break;
                default:                   agent.avoidancePriority = Random.Range(10, 30);  break;
            }
        }

        AssignLayerAndIgnoreCollisions();
    }

    private void AssignLayerAndIgnoreCollisions()
    {
        string ownLayerName = cockroachType switch
        {
            CockroachType.Small => "CockroachSmall",
            CockroachType.Large => "CockroachLarge",
            CockroachType.Other => "CockroachOther",
            _                   => "CockroachSmall"
        };

        int ownLayer = LayerMask.NameToLayer(ownLayerName);
        if (ownLayer == -1)
        {
            Debug.LogWarning($"[SmallCockroach] Layer '{ownLayerName}' not found. Add it in Edit > Project Settings > Tags and Layers.");
            return;
        }

        gameObject.layer = ownLayer;

        // Ignore collisions against every other cockroach layer.
        string[] allCockroachLayers = { "CockroachSmall", "CockroachLarge", "CockroachOther" };
        foreach (string otherLayerName in allCockroachLayers)
        {
            if (otherLayerName == ownLayerName) continue;

            int otherLayer = LayerMask.NameToLayer(otherLayerName);
            if (otherLayer != -1)
                Physics.IgnoreLayerCollision(ownLayer, otherLayer, true);
        }
    }

    private void UpdateStuckTimer(bool wantsToMove, bool isMoving)
    {
        if (wantsToMove && !isMoving)
        {
            stuckTimer += Time.deltaTime;
            return;
        }

        stuckTimer = 0f;
    }

    private System.Collections.IEnumerator TraverseLink()
    {
        RemoveWallMargin();

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;

        Vector3 startPos = agent.transform.position;
        Vector3 endPos = linkData.endPos;

        ObstacleAvoidanceType savedAvoidance = agent.obstacleAvoidanceType;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Max(distance / agent.speed, 0.05f);
        float elapsed = 0f;

        Vector3 travelDir = (endPos - startPos);
        if (travelDir.sqrMagnitude < 0.0001f)
            travelDir = transform.forward;
        travelDir = travelDir.normalized;

        // Compute the base facing rotation from the travel direction (flat, like the original).
        Vector3 surfaceRight = Vector3.Cross(travelDir, Vector3.up).normalized;
        if (surfaceRight.sqrMagnitude < 0.01f)
            surfaceRight = Vector3.Cross(travelDir, Vector3.forward).normalized;
        Vector3 surfaceUp = Vector3.Cross(surfaceRight, travelDir).normalized;
        Quaternion baseRot = Quaternion.LookRotation(travelDir, surfaceUp);

        // Compute surface tilt corrections at start and end via raycast.
        // This is only meaningful for large cockroaches on walls/ceilings.
        Quaternion startTilt = GetSurfaceTiltCorrection(startPos);
        Quaternion endTilt   = GetSurfaceTiltCorrection(endPos);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            agent.nextPosition = pos;
            transform.position = pos;

            // Slerp the tilt correction from start to end, apply on top of the base facing.
            Quaternion tiltCorrection = Quaternion.Slerp(startTilt, endTilt, t);
            Quaternion targetRot = tiltCorrection * baseRot;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);

            SetAnimatorBool("isMoving", true);

            yield return null;
        }

        agent.nextPosition = endPos;
        transform.position = endPos;
        transform.rotation = endTilt * baseRot;

        agent.obstacleAvoidanceType = savedAvoidance;
        agent.CompleteOffMeshLink();
        isTraversingLink = false;
        stuckTimer = 0f;

        ApplyWallMargin();
    }

    /// <summary>
    /// Raycasts downward from the position to find the surface normal,
    /// then returns the rotation needed to tilt from Vector3.up to that normal.
    /// Returns identity (no tilt) if nothing is hit.
    /// </summary>
    private Quaternion GetSurfaceTiltCorrection(Vector3 position)
    {
        float castDist = agent.height * 2f;

        // Cast from slightly above in the current up direction.
        Vector3 origin = position + transform.up * (agent.height * 0.5f);
        if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, castDist,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return Quaternion.FromToRotation(Vector3.up, hit.normal);

        // Fallback: straight down for flat floors.
        if (Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out hit, castDist,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return Quaternion.FromToRotation(Vector3.up, hit.normal);

        return Quaternion.identity;
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
                bool set = agent.SetDestination(destination);
                if (set)
                    randomMoveTimer = Random.Range(minRandomMoveDuration, maxRandomMoveDuration);
                return set;
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

        return agent.SetDestination(instanceGoals[currentGoalIndex].position);
    }

    private bool TryGetRandomNavMeshDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit currentHit, agent.radius * 2f, NavMesh.AllAreas))
            return false;

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (!TryGetSurfaceBasis(currentHit.position, triangulation, out Vector3 tangent, out Vector3 bitangent))
            return false;

        float minSeparation = Mathf.Max(agent.radius * 0.5f, 0.05f);
        float minSeparationSqr = minSeparation * minSeparation;

        // Only attempt local short-range candidates when the random roll misses the cross-link bias.
        if (Random.value >= crossLinkBias)
        {
            for (int attempt = 0; attempt < randomPointAttempts; attempt++)
            {
                float radius = Random.Range(minRandomMoveRadius, maxRandomMoveRadius);
                float angle = Random.Range(0f, Mathf.PI * 2f);

                Vector3 offset = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
                Vector3 candidate = currentHit.position + offset;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, randomPointSampleRadius, NavMesh.AllAreas))
                    continue;

                if ((hit.position - currentHit.position).sqrMagnitude < minSeparationSqr)
                    continue;

                NavMeshPath path = new NavMeshPath();
                agent.CalculatePath(hit.position, path);

                if (path.status == NavMeshPathStatus.PathInvalid)
                    continue;

                destination = hit.position;
                return true;
            }
        }

        // Cross-surface pass: pick a random point from the full baked NavMesh triangulation.
        if (triangulation.vertices != null && triangulation.vertices.Length > 0 &&
            triangulation.indices != null && triangulation.indices.Length >= 3)
        {
            for (int attempt = 0; attempt < randomPointAttempts; attempt++)
            {
                int triStart = Random.Range(0, triangulation.indices.Length / 3) * 3;

                Vector3 a = triangulation.vertices[triangulation.indices[triStart]];
                Vector3 b = triangulation.vertices[triangulation.indices[triStart + 1]];
                Vector3 c = triangulation.vertices[triangulation.indices[triStart + 2]];

                float r1 = Random.value;
                float r2 = Random.value;
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                Vector3 candidate = a + r1 * (b - a) + r2 * (c - a);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, randomPointSampleRadius, NavMesh.AllAreas))
                    continue;

                if ((hit.position - currentHit.position).sqrMagnitude < minSeparationSqr)
                    continue;

                NavMeshPath path = new NavMeshPath();
                agent.CalculatePath(hit.position, path);

                if (path.status == NavMeshPathStatus.PathInvalid)
                    continue;

                destination = hit.position;
                return true;
            }
        }

        return false;
    }

    private bool TryGetSurfaceBasis(Vector3 position, NavMeshTriangulation triangulation, out Vector3 tangent, out Vector3 bitangent)
    {
        tangent = Vector3.right;
        bitangent = Vector3.forward;

        if (triangulation.vertices == null || triangulation.indices == null || triangulation.indices.Length < 3)
            return false;

        float closestDistanceSqr = float.MaxValue;
        Vector3 surfaceNormal = Vector3.up;
        bool foundTriangle = false;

        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[i]];
            Vector3 b = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[i + 2]];
            Vector3 closestPoint = ClosestPointOnTriangle(position, a, b, c);
            float distanceSqr = (position - closestPoint).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            Vector3 triangleNormal = Vector3.Cross(b - a, c - a).normalized;

            if (triangleNormal.sqrMagnitude <= 0.0001f)
                continue;

            closestDistanceSqr = distanceSqr;
            surfaceNormal = triangleNormal;
            foundTriangle = true;
        }

        if (!foundTriangle)
            return false;

        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) < 0.99f
            ? Vector3.up
            : Vector3.right;

        tangent = Vector3.Cross(surfaceNormal, referenceAxis).normalized;
        bitangent = Vector3.Cross(surfaceNormal, tangent).normalized;

        return tangent.sqrMagnitude > 0.0001f && bitangent.sqrMagnitude > 0.0001f;
    }

    private Vector3 ClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + ab * v;
        }

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + ac * w;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + (c - b) * w;
        }

        float denom = 1f / (va + vb + vc);
        float vInside = vb * denom;
        float wInside = vc * denom;
        return a + ab * vInside + ac * wInside;
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(paramName, value);
                return;
            }
        }
    }

    private void ApplyWallMargin()
    {
        if (cockroachType != CockroachType.Large) return;
        agent.radius = normalAgentRadius + largeTypeWallMargin;
    }

    private void RemoveWallMargin()
    {
        if (cockroachType != CockroachType.Large) return;
        agent.radius = normalAgentRadius;
    }

    private bool IsPathBlockedByCockroach()
    {
        if (!agent.hasPath || agent.pathPending) return false;

        // Only check when actively trying to move.
        Vector3 velocity = agent.desiredVelocity;
        if (velocity.sqrMagnitude < stuckVelocityThreshold * stuckVelocityThreshold) return false;

        Vector3 origin = transform.position;
        Vector3 direction = velocity.normalized;

        // SphereCast ahead along the desired velocity direction.
        if (!Physics.SphereCast(origin, largeBlockCheckRadius, direction, out RaycastHit hit,
                largeBlockCheckDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return false;

        // Only yield to another SmallCockroach, not walls or other objects.
        return hit.collider.GetComponent<SmallCockroach>() != null;
    }
}
