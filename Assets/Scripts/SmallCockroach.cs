using UnityEngine;
using UnityEngine.AI;

public class SmallCockroach : MonoBehaviour
{
    [SerializeField] private Transform[] goals;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 4f;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform[] instanceGoals;
    private int currentGoalIndex = 0;
    private float idleTimer = 0f;
    private bool isIdling = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Disable built-in agent rotation so we control it manually
        agent.updateRotation = false;

        instanceGoals = goals != null ? (Transform[])goals.Clone() : new Transform[0];

        if (instanceGoals.Length == 0) return;

        agent.destination = instanceGoals[currentGoalIndex].position;
    }

    // Update is called once per frame
    void Update()
    {
        bool isMoving = !agent.pathPending && agent.remainingDistance > agent.stoppingDistance;
        //if animator is assigned, set the "isMoving" parameter to control animations
        //bool "isMoving"
        if (animator != null)
        {
            animator.SetBool("isMoving", isMoving);
        }

        // Manually rotate towards movement direction
        if (agent.velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (instanceGoals == null || instanceGoals.Length == 0) return;

        if (currentGoalIndex >= instanceGoals.Length - 1) return;

        if (!isMoving)
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
