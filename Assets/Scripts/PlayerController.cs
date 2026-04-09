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
    //[SerializeField] private float jumpForce = 5f;
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float maxLookAngle = 80f;

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
    [SerializeField] private AudioClip _walkingSound;
    [SerializeField] private AudioClip _sprintSound;
    [SerializeField] private AudioSource _audioSource;

    [Header("Raycast Settings")]
    [SerializeField] private float _lineofSightMaxDist;
    [SerializeField] private Vector3 _raycastStartOffset;


    //private string _npcTag = "NPC";

    [SerializeField] private Rigidbody rb;
    public Camera playerCamera;
    private Vector3 velocity;
    private bool isGrounded;
    private float verticalRotation = 0f;
    private bool _isPlayingFootsteps = false;
    // Variables for Gizmo drawing
    private Vector3 _raycastHitLocation;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
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

    private void FixedUpdate()
    {
        // Only allow jumping in Normal state
        if (currentState == PlayerState.Normal)
        {
            //HandleJump();
            //HandleWalkingSound();
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
                rb.velocity = new Vector3(0, rb.velocity.y, 0);
                isSprinting = false;
                //StopWalkingSound();
                break;

            case PlayerState.Cutscene:
                // Stop all movement and store camera state if needed
                rb.velocity = new Vector3(0, rb.velocity.y, 0);
                isSprinting = false;
                //StopWalkingSound();
                break;

            case PlayerState.Disabled:
                // Stop all movement
                rb.velocity = new Vector3(0, rb.velocity.y, 0);
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

        // Get input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // Only process movement if there's input
        if (moveX == 0 && moveZ == 0)
        {
            return;
        }

        // Check if sprinting
        isSprinting = Input.GetKey(KeyCode.LeftShift) && CanSprint && (moveX != 0 || moveZ != 0);

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

        // Player movement using transform
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        //rb.MovePosition(rb.position + move.normalized * currentSpeed * Time.fixedDeltaTime);
        transform.Translate(move.normalized * currentSpeed * Time.deltaTime, Space.World);
    }

    //private void HandleJump()
    //{
    //    // Check if grounded
    //    isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
    //    if (isGrounded)
    //    {
    //        isJumping = false;
    //    }
    //    // Jump
    //    if (Input.GetKey(KeyCode.Space) && isGrounded && !isJumping && rb.velocity.y <= 1f)
    //    {
    //        //rb force jump
    //        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); // reset y velocity before jump to prevent double jump height
    //        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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


    // Handle walking sound effects
    private void HandleWalkingSound()
    {
        // Calculate horizontal velocity magnitude (ignore vertical movement)
        //Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        float movementSpeed = move.magnitude;


        // Play footsteps if player is moving and grounded
        if (movementSpeed >= 0.1f && isGrounded)
        {
            if (!_isPlayingFootsteps && _audioSource != null && _walkingSound != null)
            {
                if (isSprinting && _sprintSound != null)
                {
                    _audioSource.clip = _sprintSound;
                }
                else
                {
                    _audioSource.clip = _walkingSound;
                }
                _audioSource.loop = true;
                _audioSource.Play();
                _isPlayingFootsteps = true;
            }
            else if (_isPlayingFootsteps && _audioSource != null)
            {
                // Switch audio clips if sprint state changed while already playing
                AudioClip targetClip = (isSprinting && _sprintSound != null) ? _sprintSound : _walkingSound;
                if (_audioSource.clip != targetClip)
                {
                    _audioSource.Stop();
                    _audioSource.clip = targetClip;
                    _audioSource.Play();
                }
            }
        }
        else
        {
            StopWalkingSound();
        }
    }

    // Stop walking sound
    private void StopWalkingSound()
    {
        if (_isPlayingFootsteps && _audioSource != null)
        {
            _audioSource.loop = false;
            _audioSource.Stop();
            _isPlayingFootsteps = false;
        }
    }
}