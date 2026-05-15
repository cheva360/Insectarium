using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class playerController : MonoBehaviour
{
    public static playerController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // player State Machine
    public enum playerState
    {
        Normal,      // Can move, interact, and enter cutscenes/dialogue
        InDialogue,  // Cannot move camera lerps to npc
        Cutscene,    // Cannot move camera controlled by cutscene
        Disabled     // no movement or interactions
    }

    [Header("State")]
    [SerializeField] private playerState currentState = playerState.Normal;
    public playerState CurrentState => currentState;

    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;
    private float baseMoveSpeed;

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
    [SerializeField] private float radarUpdateFPS = 12f;
    [SerializeField] private float radarHideSpeed = 5f;
    public bool radarHidden = false;

    [Header("Dialogue Settings")]
    [SerializeField] private float dialogueLookSpeed = 3f;
    private Transform _dialogueLookTarget;
    private Transform _dialoguePlayerTarget;
    private Vector3 _preDialoguePosition;
    private Quaternion _preDialogueRotation;
    private float _preDialogueVerticalRotation;
    private Coroutine _exitDialogueCoroutine;

    //[Header("Sprint Settings")]
    //[SerializeField] private float sprintDuration = 2f;
    //[SerializeField] private Image SprintBar;
    //[SerializeField] private Image SprintBarBackground;
    //[SerializeField] private float alphaBlinkSpeed = 2f;

    public float Sprint = 1f;
    private bool CanSprint;
    private bool isSprinting = false;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip _footstepSound1;
    [SerializeField] private AudioClip _footstepSound2;
    [SerializeField] private AudioClip _sprintSound;
    [SerializeField] private AudioSource _audioSource;

    [Header("Raycast Settings")]
    [SerializeField] private float _lineofSightMaxDist;
    [SerializeField] private Vector3 _raycastStartOffset;

    private CharacterController characterController;
    public Camera playerCamera;
    private Vector3 velocity;
    private bool isGrounded;
    private float verticalRotation = 0f;
    private bool _isPlayingFootsteps = false;
    private Vector3 _raycastHitLocation;

    // Radar animation variables
    private Vector3 radarOriginalPosition;
    private float radarSwayOffset = 0f;
    private float radarSwayVelocity = 0f;
    private float bobTimer = 0f;
    private Vector3 currentBobOffset = Vector3.zero;
    private float radarUpdateAccumulator = 0f;
    private float _accumulatedMouseX = 0f;
    private float _radarCurrentY = -0.03f;

    // Audio fade variables
    private Coroutine _fadeOutCoroutine;
    private float _originalVolume = 1f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerCamera = cameraTransform.GetComponent<Camera>();

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource != null)
            _originalVolume = _audioSource.volume;

        if (radar3DModel != null)
        {
            radarOriginalPosition = radar3DModel.localPosition;
            _radarCurrentY = radarOriginalPosition.y;
        }

        baseMoveSpeed = moveSpeed;
    }

    private Vector3 _raycastStart => cameraTransform.position;
    private Vector3 _raycastDir => (cameraTransform.forward).normalized;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Always apply radar Y position every frame (lerp + bob are calculated on radar tick)
        if (radar3DModel != null)
        {
            Vector3 pos = radar3DModel.localPosition;
            pos.y = _radarCurrentY + currentBobOffset.y;
            radar3DModel.localPosition = pos;
        }

        // Radar animation always ticks regardless of state
        HandleRadarAnimation();

        switch (currentState)
        {
            case playerState.Normal:
                HandleMovement();
                HandleMouseLook();
                HandleWalkingSound();
                break;

            case playerState.InDialogue:
                LerpCameraToTarget();
                break;

            case playerState.Cutscene:
                break;

            case playerState.Disabled:
                break;
        }
    }

    public void EnterDialogue(Transform lookTarget, Transform playerTarget = null)
    {
        _preDialoguePosition = transform.position;
        _preDialogueRotation = transform.rotation;
        _preDialogueVerticalRotation = verticalRotation;

        _dialogueLookTarget = lookTarget;
        _dialoguePlayerTarget = playerTarget;
        SetState(playerState.InDialogue);
    }

    public void ExitDialogue()
    {
        radarHidden = false;
        if (_exitDialogueCoroutine != null)
            StopCoroutine(_exitDialogueCoroutine);
        _exitDialogueCoroutine = StartCoroutine(ExitDialogueCoroutine());
    }

    private IEnumerator ExitDialogueCoroutine()
    {
        _dialogueLookTarget = null;
        _dialoguePlayerTarget = null;

        while (Vector3.Distance(transform.position, _preDialoguePosition) > 0.01f ||
               Quaternion.Angle(transform.rotation, _preDialogueRotation) > 0.5f ||
               Mathf.Abs(verticalRotation - _preDialogueVerticalRotation) > 0.5f)
        {
            transform.position = Vector3.Lerp(transform.position, _preDialoguePosition, dialogueLookSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _preDialogueRotation, dialogueLookSpeed * Time.deltaTime);
            verticalRotation = Mathf.Lerp(verticalRotation, _preDialogueVerticalRotation, dialogueLookSpeed * Time.deltaTime);
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            yield return null;
        }

        transform.position = _preDialoguePosition;
        transform.rotation = _preDialogueRotation;
        verticalRotation = _preDialogueVerticalRotation;
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        SetState(playerState.Normal);
        _exitDialogueCoroutine = null;
    }

    private void LerpCameraToTarget()
    {
        if (_dialoguePlayerTarget != null)
            transform.position = Vector3.Lerp(transform.position, _dialoguePlayerTarget.position, dialogueLookSpeed * Time.deltaTime);

        if (_dialogueLookTarget == null) return;

        Vector3 directionToTarget = (_dialogueLookTarget.position - transform.position).normalized;
        Quaternion targetBodyRotation = Quaternion.Euler(0f, Quaternion.LookRotation(directionToTarget, Vector3.up).eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetBodyRotation, dialogueLookSpeed * Time.deltaTime);

        float targetVertical = -Mathf.Asin(directionToTarget.y) * Mathf.Rad2Deg;
        targetVertical = Mathf.Clamp(targetVertical, -maxLookAngle, maxLookAngle);
        verticalRotation = Mathf.Lerp(verticalRotation, targetVertical, dialogueLookSpeed * Time.deltaTime);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    public void SetState(playerState newState)
    {
        if (currentState == newState) return;
        OnStateExit(currentState);
        currentState = newState;
        OnStateEnter(newState);
    }

    private void OnStateEnter(playerState state)
    {
        switch (state)
        {
            case playerState.Normal:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                characterController.enabled = true;
                break;

            case playerState.InDialogue:
                velocity = Vector3.zero;
                isSprinting = false;
                break;

            case playerState.Cutscene:
                characterController.enabled = false;
                velocity = Vector3.zero;
                isSprinting = false;
                StopWalkingSound();
                break;

            case playerState.Disabled:
                velocity = Vector3.zero;
                isSprinting = false;
                break;
        }
    }

    private void OnStateExit(playerState state)
    {
        switch (state)
        {
            case playerState.InDialogue:
                _dialogueLookTarget = null;
                _dialoguePlayerTarget = null;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;
        }
    }

    private void HandleMovement()
    {
        isGrounded = characterController.isGrounded;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        characterController.Move(move.normalized * currentSpeed * Time.deltaTime);

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    private void HandleRadarAnimation()
    {
        if (radar3DModel == null) return;

        _accumulatedMouseX += Input.GetAxis("Mouse X") * mouseSensitivity;

        float radarDeltaTime = 1f / Mathf.Max(radarUpdateFPS, 1f);
        radarUpdateAccumulator += Time.deltaTime;

        if (radarUpdateAccumulator < radarDeltaTime)
            return;

        float tickDelta = radarUpdateAccumulator;
        radarUpdateAccumulator = 0f;

        // Lerp radar Y based on radarHidden — same FPS as all radar animations
        float targetRadarY = radarHidden ? -0.8f : -0.03f;
        _radarCurrentY = Mathf.Lerp(_radarCurrentY, targetRadarY, radarHideSpeed * tickDelta);

        // Only apply sway and bob in Normal state
        if (currentState != playerState.Normal)
            return;

        float mouseX = _accumulatedMouseX;
        _accumulatedMouseX = 0f;

        float targetSwayOffset = -mouseX * radarSwayAmount;
        radarSwayOffset = Mathf.SmoothDamp(radarSwayOffset, targetSwayOffset, ref radarSwayVelocity, 1f / radarSwaySmooth, Mathf.Infinity, tickDelta);
        radarSwayOffset = Mathf.Lerp(radarSwayOffset, 0f, radarSwayDamping * tickDelta);

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        bool isMoving = (moveX != 0 || moveZ != 0) && isGrounded;

        Vector3 targetBobOffset = Vector3.zero;

        if (isMoving)
        {
            float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
            float speedMultiplier = currentSpeed / baseMoveSpeed;
            bobTimer += tickDelta * radarBobFrequency * speedMultiplier;
            float horizontalBob = Mathf.Sin(bobTimer) * radarBobHorizontalAmount;
            float verticalBob = Mathf.Sin(bobTimer * 2f) * radarBobVerticalAmount;
            targetBobOffset = new Vector3(horizontalBob, verticalBob, 0f);
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, radarReturnToNeutralSpeed * tickDelta);
        }

        currentBobOffset = Vector3.Lerp(currentBobOffset, targetBobOffset, radarReturnToNeutralSpeed * tickDelta);

        Vector3 swayOffset = new Vector3(radarSwayOffset, 0f, 0f);
        radar3DModel.localPosition = new Vector3(
            radarOriginalPosition.x + swayOffset.x + currentBobOffset.x,
            _radarCurrentY + currentBobOffset.y,
            radarOriginalPosition.z + currentBobOffset.z
        );
    }

    private void LerpToNPC() { }

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
                _footstepCoroutine = StartCoroutine(FootstepLoop());
        }
    }

    private IEnumerator FootstepLoop()
    {
        _isPlayingFootsteps = true;
        _nextFootstep = 0;
        const float defaultMoveSpeed = 3.5f;

        while (true)
        {
            if (!IsWalking()) break;

            AudioClip clip = (_nextFootstep == 0) ? _footstepSound1 : _footstepSound2;
            _nextFootstep = (_nextFootstep + 1) % 2;

            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
                float currentSpeed = Mathf.Max(moveSpeed, 0.1f);
                float extraDelay = clip.length * ((defaultMoveSpeed / currentSpeed) - 1f);
                yield return new WaitForSeconds(clip.length + extraDelay);
            }
            else
            {
                yield return null;
            }

            if (!IsWalking()) break;
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

    private bool _isGameFocused = true;
}