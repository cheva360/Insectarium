using UnityEngine;
using UnityEngine.AI;

public class SmallCockroach : MonoBehaviour
{
    [SerializeField] private Transform[] goals;
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Disable built-in agent rotation so we control it manually
        agent.updateRotation = false;
        // Manually traverse off-mesh links to keep consistent speed
        agent.autoTraverseOffMeshLink = false;

        instanceGoals = goals != null ? (Transform[])goals.Clone() : new Transform[0];

        if (instanceGoals.Length == 0) return;

        if (hasStartedMoving)
            agent.destination = instanceGoals[currentGoalIndex].position;
    }

    // Update is called once per frame
    void Update()
    {
        if (!hasStartedMoving) return;

        //if has navmesh
        if (agent != null)
        {
            // Set destination on the first frame hasStartedMoving becomes true
            if (!agent.hasPath && !agent.pathPending && !isTraversingLink && instanceGoals != null && instanceGoals.Length > 0)
                agent.destination = instanceGoals[currentGoalIndex].position;

            // Manually traverse off-mesh links at normal agent speed
            if (agent.isOnOffMeshLink && !isTraversingLink)
            {
                isTraversingLink = true;
                StartCoroutine(TraverseLink());
                return;
            }

            if (isTraversingLink) return;

            bool isMoving = !agent.pathPending && agent.remainingDistance > agent.stoppingDistance;

            if (animator != null)
            {
                animator.SetBool("isMoving", isMoving);
            }

            HandleRotation();

            if (instanceGoals == null || instanceGoals.Length == 0) return;

            if (currentGoalIndex >= instanceGoals.Length - 1) return;

            if (!isMoving)
            {
                if (!stopOnGoal)
                {
                    // Skip wait time, immediately move to next goal
                    currentGoalIndex++;
                    agent.destination = instanceGoals[currentGoalIndex].position;
                }
                else
                {
                    if (!isIdling)
                    {
                        // Just arrived, start idle timer
                        isIdling = true;
                        idleTimer = Random.Range(minIdleTime, maxIdleTime);
                    }
                    else
                    {
                        idleTimer -= Time.deltaTime;

                        if (idleTimer <= 0f)
                        {
                            // Done idling, move to next goal
                            isIdling = false;
                            currentGoalIndex++;
                            agent.destination = instanceGoals[currentGoalIndex].position;
                        }
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator TraverseLink()
    {
        OffMeshLinkData linkData = agent.currentOffMeshLinkData;

        // Agent already stopped at link start (autoTraverseOffMeshLink = false),
        // so use the link's geometry for an accurate direction
        Vector3 startPos = transform.position;
        Vector3 endPos   = linkData.endPos + Vector3.up * agent.baseOffset;

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
}
