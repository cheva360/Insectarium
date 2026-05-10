using UnityEngine;
using UnityEngine.AI;
 
/// <summary>
/// Attach this script to an NPC GameObject that has a NavMeshAgent component.
/// The NPC will follow the player but enter an idle state when within
/// the idleDistance threshold, resuming movement once the player moves away.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCFollower : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Transform the NPC should follow. Assign your Player here.")]
    public Transform player;
 
    [Header("Follow Settings")]
    [Tooltip("Distance (in meters) at which the NPC stops and goes idle.")]
    public float idleDistance = 2f;
 
    [Tooltip("How often (in seconds) the NavMesh destination is recalculated.")]
    public float updateRate = 0.1f;
 
    // ── Internal state ────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private bool         _isIdle;
    private float        _updateTimer;
 
    // Optional: wire these up in the Inspector if you're using an Animator
    [Header("Animation (Optional)")]
    [Tooltip("Animator on this NPC. Leave empty if not using animations.")]
    public Animator animator;
 
    [Tooltip("Name of the bool parameter in the Animator that controls idle state.")]
    public string idleAnimBoolName = "IsIdle";
 
    // ─────────────────────────────────────────────────────────────────────────
 
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
 
    private void Start()
    {
        if (player == null)
        {
            Debug.LogWarning("[NPCFollower] No player Transform assigned. " +
                             "Please assign one in the Inspector.");
        }
 
        _isIdle      = false;
        _updateTimer = 0f;
    }
 
    private void Update()
    {
        if (player == null) return;
 
        // Throttle destination updates to avoid excessive NavMesh calls
        _updateTimer += Time.deltaTime;
        if (_updateTimer < updateRate) return;
        _updateTimer = 0f;
 
        EvaluateFollowBehavior();
    }
 
    /// <summary>
    /// Core logic: measure distance and switch between follow and idle.
    /// </summary>
    private void EvaluateFollowBehavior()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
 
        if (distanceToPlayer <= idleDistance)
        {
            // --- Enter / stay in IDLE ---
            if (!_isIdle)
            {
                _isIdle = true;
                _agent.ResetPath();       // Stop moving immediately
                _agent.isStopped = true;  // Prevent resuming via velocity
                SetAnimatorIdle(true);
            }
        }
        else
        {
            // --- Enter / stay in FOLLOW ---
            if (_isIdle)
            {
                _isIdle          = false;
                _agent.isStopped = false;
                SetAnimatorIdle(false);
            }
 
            _agent.SetDestination(player.position);
        }
    }
 
    /// <summary>
    /// Safely sets the idle animation parameter if an Animator is assigned.
    /// </summary>
    private void SetAnimatorIdle(bool idle)
    {
        if (animator != null && !string.IsNullOrEmpty(idleAnimBoolName))
        {
            animator.SetBool(idleAnimBoolName, idle);
        }
    }
 
    // ── Gizmo: visualise the idle radius in the Scene view ───────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, idleDistance);
    }
}