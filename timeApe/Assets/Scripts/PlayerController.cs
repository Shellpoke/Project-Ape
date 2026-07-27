using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public Transform modelTransform; //The Character Model Goes here in the editor
    public float slowSpeed = 1f;
    public float fastSpeed = 20f;
    public float acceleration = 40f;
    public float rotationSpeed = 10f;
    public float modelRotateSpeed = 10f;
    public float deadZone = 0.4f;
    public float blockedTimer = 1f;
    public float skidTolerance = -0.7f;

    [Header("Jumping")]
    public float jumpHeight = 10f;
    public float gravity = -90f;
    public float jumpRelease = 2.5f;
    public float coyoteTimer = 0.2f;
    public float backflipSpeed = 10f;
    public float backflipHeight = 10f;

    [Header("Camera")]
    public Transform cameraTransform; //The Camera pivot goes here in editor

    [Header("Spin")]
    public float jumpSpinHeight = 20f;
    public float spinTimeout = 0.20f;
    public float spinAngleTrigger = 720f;

    //private variables
    private bool walker;
    private bool jumpHold;
    private bool jumped;
    private bool isSpinning = false;
    private float verticalLookRotation;
    private float speed;
    private float rotationCheck = 0f;
    private float spinCooldownTimer = 0f;
    private float clockoyote = 0f;
    private int spinDirection = 0;
    private CharacterController controller;
    private PlayerInput inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 previousStick = Vector2.zero;
    private Vector3 velocity;
    private Vector3 moveDirection;



    public float skidDuration = 0.2f;
    public float skidDeceleration = 30f;

    private bool isSkidding;
    private float skidTimer;

    private Vector3 lastMoveDirection;



    private bool isBackflipping;
    private bool movementBlocked;
    private float movementBlockTimer;
    private Vector3 launchDirection;



    /*
    --------------------------------------- UNITY'S EXECUTION FUNCTIONS -----------------------------------------------------------------------
    */

    void Awake()
    {
        controller = GetComponent<CharacterController>(); //Start taking information from controller input
        inputActions = new PlayerInput();
    }

    void OnEnable()
    {
        inputActions.Enable();

        //Connecting Left Joystick and WASD
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        //Connecting Right Joystick and Mouse
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        //Connecting Sprint Alternative for Keyboard
        inputActions.Player.Sprint.performed += ctx => walker = true;
        inputActions.Player.Sprint.canceled += ctx => walker = false;

        //Connecting Jump press
        inputActions.Player.Jump.performed += ctx =>
        {
            jumpHold = true;
            Jump();
        };

        inputActions.Player.Jump.canceled += ctx =>
        {
            jumpHold = false;
        };
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        RotateCamera();
        RotateModel();

        //Makes movement obey deadzones
        if (moveInput.magnitude > deadZone)
        {
            moveDirection = MoveDirect();
        }

        //defines coyote time to jump
        if (controller.isGrounded || !jumped)
        {
            clockoyote = coyoteTimer;
        }
        else
        {
            clockoyote -= Time.deltaTime;
        }

        ApplyGravity();
        DetectSpin();

        if (movementBlocked) //if the boolean is not on, allow player to move by ignoring this function
        {
            MoveanBlock();
        }
        else // if there is no blockage move as usual
        {
            Move();
        }
        Debug.Log(clockoyote);
    }

    /*
     --------------------------------------- MAIN FUNCTIONS ----------------------------------------------------------------------------------
     */

    Vector3 MoveDirect() //RESPONSIBLE OF HORIZONTAL MOVEMENT
    {
        if (isBackflipping) //ignore user's joystick direction while backflipping
        {
            return launchDirection;
        }

        //Making movement based on camera perception
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;    
        //set vertical values 0 and normalize them, this is more a preventive measurement
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return (forward * moveInput.y + right * moveInput.x).normalized;
    }


    void Move() //HANDLES HORIZONTAL MOVEMENT SPEED
    {
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
        float midSpeed;
        //ifelse used to let keyboard users walk using shift.
        if (!controller.isGrounded)
        {
            isSkidding = false;
        }

        if (walker || isSpinning)
        {
            midSpeed = slowSpeed;
        }

        else if (inputMagnitude > deadZone)
        {
            midSpeed = Mathf.Lerp(slowSpeed, fastSpeed, inputMagnitude); //Lerp handles the speed scaling from the joystick
        }
        else
        {
            midSpeed = 0f;
        }

        if (!isSkidding && speed > 2f && moveDirection.sqrMagnitude > 0.01f && lastMoveDirection.sqrMagnitude > 0.01f) //skidcheck
        {
            float dot = Vector3.Dot(lastMoveDirection, moveDirection);

            if (dot < skidTolerance)
            {
                isSkidding = true;
                skidTimer = skidDuration;
            }
        }

        if (isSkidding) //when the skid state starts, run down the timer, perform the skid and leave the skid state once the timer has passed
        {
            skidTimer -= Time.deltaTime;

            speed = Mathf.MoveTowards(speed, 0f, skidDeceleration * Time.deltaTime);

            controller.Move(lastMoveDirection * speed * Time.deltaTime);

            if (skidTimer <= 0f)
                isSkidding = false;
            return;
        }

        speed = Mathf.MoveTowards(speed, midSpeed, acceleration * Time.deltaTime);
        controller.Move(moveDirection * speed * Time.deltaTime); //actual moving registered

        if (speed > 0.1f && moveDirection.sqrMagnitude > 0.1f) //detection of the direction
        {
            lastMoveDirection = moveDirection;
        }
    }


    void ApplyGravity() //HANDLES VERTICAL SPEED
    {
        //this resets vertical speed when a player collides with a ceiling
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime * 2f);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0)
        {
            velocity.y = 0f;
            movementBlockTimer = 0f; //interrupts special jumps if a wall is hit
        }

        //this maintains the player grounded
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumped = false;
        }

        //this implements the cut in jumping for variable jump
        if (velocity.y > 0f && !jumpHold)
        {
            velocity.y += gravity * Time.deltaTime * jumpRelease;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    void Jump() //HANDLES CHANGES IN VERTICAL SPEED CAUSED BY JUMPING
    {
        if (clockoyote > 0f) //preventing user from jumping more than once
        {
            if (isSpinning && controller.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpSpinHeight * -2f * gravity);
                jumped = true;
            }

            else
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                clockoyote = 0f;
                jumped = true;
            }

            if (isSkidding) //gets the player of the skid state to enter the backflip state
            {
                isSkidding = false;
                speed = 0f;
                StartBackflip();

            }

            //spin lock to ensure is always grounded
            isSpinning = false;
            rotationCheck = 0f;
            spinDirection = 0;
            previousStick = Vector2.zero;
        }
    }

    void DetectSpin() //DETECTS IF THE PLAYER IS ATTEMPTING TO GET INTO THE SPINNING STATE
    {
        if (controller.isGrounded)
        {
            if (moveInput.magnitude < 0.7f)
            {

                previousStick = Vector2.zero;
                rotationCheck = 0f;
                spinDirection = 0;
                isSpinning = false;
                return;
            }

            if (previousStick != Vector2.zero)
            {
                float angleDelta =
                    Vector2.SignedAngle(previousStick, moveInput);

                if (Mathf.Abs(angleDelta) > 2f)
                {
                    spinCooldownTimer = spinTimeout;
                    int currentDirection = angleDelta > 0 ? 1 : -1;

                    if (spinDirection == 0)
                    {
                        // first detected rotation
                        spinDirection = currentDirection;
                    }

                    if (currentDirection == spinDirection)
                    {
                        // same rotation direction → keep charging
                        rotationCheck += Mathf.Abs(angleDelta);
                    }
                    else
                    {
                        // reversed direction → reset spin progress
                        rotationCheck = 0f;
                        spinDirection = currentDirection;
                    }
                }
                spinCooldownTimer -= Time.deltaTime;

                if (spinCooldownTimer <= 0f)
                {
                    isSpinning = false;
                }


                if ((rotationCheck) >= spinAngleTrigger)
                {
                    isSpinning = true;
                }
            }
            previousStick = moveInput;
        }
    }


    void RotateCamera() //RESPONSIBLE OF THE CAMERA ROTATION AROUND THE MODEL.
    {
        float lookSensitivity = 120f;

        float mouseX = lookInput.x * lookSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * lookSensitivity * Time.deltaTime;

        cameraTransform.parent.Rotate(0f, mouseX, 0f);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -70f, 70f);

        cameraTransform.localRotation =
            Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }


    void RotateModel() //RESPONSIBLE OF ROTATING THE MODEL ACCORDING TO THE DIRECTION THE PLAYER IS MOVING
    {
        if (moveDirection.magnitude > 0.1f && !isBackflipping) //Moves only when joystick is tilted OR blocks when the player is doing a backflip
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, modelRotateSpeed * Time.deltaTime);
        }
    }

    void StartBackflip() //RESPONSIBLE OF THE BACKFLIP MECHANIC
    {
        //prevents extra movements and extra jumps during the backflip
        jumped = true;
        clockoyote = 0f;
        launchDirection = MoveDirect();

        //begins block movement and sets the timer
        movementBlocked = true;
        isBackflipping = true;
        movementBlockTimer = blockedTimer;

        //sets direction and height for the backflip.
        velocity.x = launchDirection.x * backflipSpeed;
        velocity.y = Mathf.Sqrt(backflipHeight * -2f * gravity);
        velocity.z = launchDirection.z * backflipSpeed;
    }

    void MoveanBlock() //RESPONSIBLE OF BLOCKING USER MOVEMENT WHEN PERFORMING CERTAIN TYPES OF JUMPS
    {

        movementBlockTimer -= Time.deltaTime; //start the blocked movement period
        
        //then reset everything once the time is up or if the player prematurely gets on a platform
        if (movementBlockTimer <= 0f || controller.isGrounded)
        {
            movementBlocked = false;

            if (isBackflipping)
            {
                isBackflipping = false;
                velocity.x = 0f;
                velocity.z = 0f;
                launchDirection = Vector3.zero;
            }

        }
    }
}