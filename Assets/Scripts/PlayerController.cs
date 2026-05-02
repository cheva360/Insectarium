using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    // Player State Machine
    public enum PlayerState
    {
        Normal,      // Can move, interact, and enter cutscenes/dialogue
        InDialogue,  // Cannot move camera lerps to npc
        Cutscene,    // Cannot move camera controlled by cutscene
        Disabled     // no movement or interactions
    }


    [Header("State")]
    [SerializeField] private PlayerState currentState = PlayerState.Normal;
    // Public property to get current state
    public PlayerState CurrentState => currentState;

    // Tracks the current quest condition

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;
    //[SerializeField] private float jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private Transform radar3DModel;

    [Header("Radar Animation Settings")]
    [SerializeField] private float radarSwayAmount = 0.05f;
    [SerializeField] private float radarSwaySmooth = 8f;
    [SerializeField] private float radarSwayDamping = 5f;
    [SerializeField] private float radarBobFrequency = 10f;
    [SerializeField] private float radarBobHorizontalAmount = 0.02f;
    [SerializeField] private float radarBobVerticalAmount = 0.03f;
    [SerializeField] private float radarReturnToNeutralSpeed = 3f;


    //[Header("Sprint Settings")]
    //[SerializeField] private float sprintDuration = 2f;
    //[SerializeField] private Image SprintBar;
    //[SerializeField] private Image SprintBarBackground;
    //[SerializeField] private float alphaBlinkSpeed = 2f;

    public float Sprint = 1f;
    private bool CanSprint;
    private bool isSprinting = false;
    //private bool isJumping = true;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip _footstepSound1;
    [SerializeField] private AudioClip _footstepSound2;
    [SerializeField] private AudioClip _sprintSound;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private float _audioFadeOutDuration = 0.2f;

    [Header("Raycast Settings")]
    [SerializeField] private float _lineofSightMaxDist;
    [SerializeField] private Vector3 _raycastStartOffset;


    //private string _npcTag = "NPC";

    private CharacterController characterController;
    public Camera playerCamera;
    private Vector3 velocity;
    private bool isGrounded;
    private float verticalRotation = 0f;
    private bool _isPlayingFootsteps = false;
    // Variables for Gizmo drawing
    private Vector3 _raycastHitLocation;

    // Radar animation variables
    private Vector3 radarOriginalPosition;
    private float radarSwayOffset = 0f;
    private float radarSwayVelocity = 0f;
    private float bobTimer = 0f;
    private Vector3 currentBobOffset = Vector3.zero;

    // Audio fade variables
    private Coroutine _fadeOutCoroutine;
    private float _originalVolume = 1f;


    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Get camera component
        playerCamera = cameraTransform.GetComponent<Camera>();

        // Get AudioSource if not assigned
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        // Store original volume
        if (_audioSource != null)
        {
            _originalVolume = _audioSource.volume;
        }

        // Store radar's original local position
        if (radar3DModel != null)
        {
            radarOriginalPosition = radar3DModel.localPosition;
        }

        // Auto-assign main camera if not set


    }

    // Raycasting Methods
    // Vector setting Ray start position to camera's world space position
    private Vector3 _raycastStart
    {
        get
        {
            return cameraTransform.position;
        }
    }

    // Vector pointing out from camera
    private Vector3 _raycastDir
    {
        get
        {
            return (cameraTransform.forward).normalized;
        }
    }

    // Update is called once per frame
    void Update()
    {

        // Update behavior based on current state
        switch (currentState)
        {
            case PlayerState.Normal:
                HandleMovement();
                HandleMouseLook();
                HandleRadarAnimation();
                HandleWalkingSound();
                //HandleSprint();
                break;

            case PlayerState.InDialogue:
                // no movement, camera lerp to npc
                //StopWalkingSound();
                //LerpToNPC();
                break;

            case PlayerState.Cutscene:
                // Camera is controlled by cutscene, no player input
                //StopWalkingSound();
                break;

            case PlayerState.Disabled:
                // No interactions or movement
                //StopWalkingSound();
                break;
        }

    }

    // Change the player's state
    public void SetState(PlayerState newState)
    {
        if (currentState == newState) return;

        // Exit current state
        OnStateExit(currentState);

        // Change state
        currentState = newState;

        // Enter new state
        OnStateEnter(newState);
    }

    // Called when entering a new state
    private void OnStateEnter(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Normal:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case PlayerState.InDialogue:
                // Stop any movement
                velocity = Vector3.zero;
                isSprinting = false;
                //StopWalkingSound();
                break;

            case PlayerState.Cutscene:
                // Stop all movement and store camera state if needed
                velocity = Vector3.zero;
                isSprinting = false;
                //StopWalkingSound();
                break;

            case PlayerState.Disabled:
                // Stop all movement
                velocity = Vector3.zero;
                isSprinting = false;
                //StopWalkingSound();
                break;
        }
    }

    // Called when exiting a state
    private void OnStateExit(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.InDialogue:
                // Restore camera control to player
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;
        }
    }

    private void HandleMovement()
    {
        // Check if grounded
        isGrounded = characterController.isGrounded;

        // Get input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // Check if sprinting
        //isSprinting = Input.GetKey(KeyCode.LeftShift) && CanSprint && (moveX != 0 || moveZ != 0);

        // Calculate current speed
        float currentSpeed;
        if (isSprinting)
        {
            currentSpeed = sprintSpeed;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        // Calculate movement direction relative to player rotation
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Move the character
        characterController.Move(move.normalized * currentSpeed * Time.deltaTime);

        // Apply gravity
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    //private void HandleJump()
    //{
    //    // Check if grounded
    //    isGrounded = characterController.isGrounded;
    //    if (isGrounded)
    //    {
    //        isJumping = false;
    //    }
    //    // Jump
    //    if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isJumping)
    //    {
    //        velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
    //        isJumping = true;
    //        //StopWalkingSound();
    //    }
    //}

    private void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player horizontally
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera vertically
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleRadarAnimation()
    {
        if (radar3DModel == null) return;

        // Get mouse input for sway
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;

        // Calculate target sway offset based on horizontal camera movement
        float targetSwayOffset = -mouseX * radarSwayAmount;

        // Use SmoothDamp for buttery smooth sway with velocity-based damping
        radarSwayOffset = Mathf.SmoothDamp(radarSwayOffset, targetSwayOffset, ref radarSwayVelocity, 1f / radarSwaySmooth, Mathf.Infinity, Time.deltaTime);

        // Apply additional damping to return to center when no input
        radarSwayOffset = Mathf.Lerp(radarSwayOffset, 0f, radarSwayDamping * Time.deltaTime);

        // Check if player is moving
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        bool isMoving = (moveX != 0 || moveZ != 0) && isGrounded;

        Vector3 targetBobOffset = Vector3.zero;

        if (isMoving)
        {
            // Increment bob timer
            float speedMultiplier = isSprinting ? 1.5f : 1f;
            bobTimer += Time.deltaTime * radarBobFrequency * speedMultiplier;

            // Calculate bob offsets using sine waves
            float horizontalBob = Mathf.Sin(bobTimer) * radarBobHorizontalAmount;
            float verticalBob = Mathf.Sin(bobTimer * 2f) * radarBobVerticalAmount;

            targetBobOffset = new Vector3(horizontalBob, verticalBob, 0f);

        }
        else
        {
            // Slowly reset bob timer when not moving to avoid sudden jumps
            bobTimer = Mathf.Lerp(bobTimer, 0f, radarReturnToNeutralSpeed * Time.deltaTime);
        }

        // Smoothly lerp current bob offset towards target (includes returning to zero)
        currentBobOffset = Vector3.Lerp(currentBobOffset, targetBobOffset, radarReturnToNeutralSpeed * Time.deltaTime);

        // Combine all offsets: original position + sway (X) + bob (X, Y)
        Vector3 swayOffset = new Vector3(radarSwayOffset, 0f, 0f);
        radar3DModel.localPosition = radarOriginalPosition + swayOffset + currentBobOffset;
    }

    private void LerpToNPC()
    {
        //// Raycast to find NPC position
        //RaycastHit hitInfo;
        //if (Physics.Raycast(_raycastStart, _raycastDir, out hitInfo, _lineofSightMaxDist))
        //{
        //    if (hitInfo.collider.gameObject.tag.Equals(_npcTag) && hitInfo.collider.gameObject.GetComponent<NPC>().enabled == true)
        //    {
        //        Vector3 npcPosition = hitInfo.collider.gameObject.transform.position;
        //        Vector3 directionToNPC = (npcPosition - cameraTransform.position).normalized;
        //        Quaternion targetRotation = Quaternion.LookRotation(directionToNPC, Vector3.up);
        //        NPCDetected?.Invoke(hitInfo.collider.gameObject);

        //        //slerp player horizontal rotate to npc
        //        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f), fovTransitionSpeed * Time.deltaTime);


        //        //slerp camera vertical rotation to npc
        //        float targetVertical = -15f;

        //        // Clamp the target rotation to prevent exceeding look angle limits
        //        //targetVertical = Mathf.Clamp(targetVertical, -maxLookAngle, maxLookAngle);

        //        // Lerp towards target
        //        verticalRotation = Mathf.Lerp(verticalRotation, targetVertical, fovTransitionSpeed * Time.deltaTime);

        //        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        //        ArrowCameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        //    }
        //}
    }


    // Track which footstep plays next
    private int _nextFootstep = 0;
    private Coroutine _footstepCoroutine;

    private bool IsWalking()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        return move.magnitude >= 0.1f && isGrounded;
    }

    private void HandleWalkingSound()
    {
        if (IsWalking())
        {
            if (_footstepCoroutine == null && _audioSource != null)
            {
                _footstepCoroutine = StartCoroutine(FootstepLoop());
            }
        }
    }

    private IEnumerator FootstepLoop()
    {
        _isPlayingFootsteps = true;
        _nextFootstep = 0;

        while (true)
        {
            if (!IsWalking())
                break;

            AudioClip clip = (_nextFootstep == 0) ? _footstepSound1 : _footstepSound2;
            _nextFootstep = (_nextFootstep + 1) % 2;

            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();

                // Wait for the clip to finish naturally — no early stop
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                yield return null;
            }

            // Clip finished — only continue to next footstep if still walking
            if (!IsWalking())
                break;
        }

        _isPlayingFootsteps = false;
        _footstepCoroutine = null;
    }

    private void StopWalkingSound()
    {
        if (_footstepCoroutine != null)
        {
            StopCoroutine(_footstepCoroutine);
            _footstepCoroutine = null;
        }
        _audioSource.Stop();
        _isPlayingFootsteps = false;
    }
}