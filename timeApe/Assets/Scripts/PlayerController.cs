using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraTransform; //The Camera pivot goes here in editor

    [Header("Jumping")]
    public float coyoteTimer = 0.2f;
    public float gravity = -90f;
    public float jumpHeight = 10f;
    public float jumpRelease = 2.5f;
    [Space(10)] //backflip variables
    public float backflipSpeed = 10f;
    public float backflipHeight = 10f;
    [Space(10)] //walljump variables
    public float groundCheckDistance = 2f;
    public float wallCheckDistance = 0.7f;
    public float wallJumpLock = 0.2f;
    public float wallJumpHorizontal = 10f;
    public float wallJumpVertical = 10f;

    [Header("Movement")]
    public Transform modelTransform; //The Character Model Goes here in the editor
    public float acceleration = 40f;
    public float blockedTimer = 1f;
    public float deadZone = 0.4f;
    public float fastSpeed = 20f;
    public float slowSpeed = 1f;
    public float rotationSpeed = 10f;
    public float modelRotateSpeed = 10f;
    [Space(10)] //skid variables
    public float skidTolerance = -0.7f;
    public float skidDuration = 0.2f;
    public float skidDeceleration = 30f;

    [Header("Spin")]
    public float jumpSpinHeight = 20f;
    public float spinAngleTrigger = 720f;
    public float spinTimeout = 0.20f;
    

    //private variables
    private bool isSkidding;
    private bool jumped;
    private bool jumpHold;
    private bool walker;
    private bool isSpinning = false;
    private bool isBackflipping;
    private bool movementBlocked;
    private bool isTouchingWall;
    private bool isWallJumping;
    private bool isNearGround;

    private float verticalLookRotation;
    private float speed;
    private float rotationCheck = 0f;
    private float spinCooldownTimer = 0f;
    private float clockoyote = 0f;
    private float skidTimer;
    private float movementBlockTimer;

    private int spinDirection = 0;

    private CharacterController controller;

    private PlayerInput inputActions;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 previousStick = Vector2.zero;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 lastMoveDirection;
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
        else
        {
            moveDirection = Vector3.zero;
        }

        ApplyGravity();

        if (movementBlocked) //if the boolean is not on, allow player to move by ignoring this function
        {
            MoveanBlock();
        }
        else // if there is no blockage move as usual
        {
            Move();
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

        isNearGround = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance); //checks if the player is near ground

        if (!controller.isGrounded) //checks for walls while jumping, no need to check them during ground state.
        {
            CheckWall();
        }

        DetectSpin();
    }











    

    /*
     --------------------------------------- MAIN FUNCTIONS ----------------------------------------------------------------------------------
     */

    Vector3 MoveDirect() //RESPONSIBLE OF HORIZONTAL MOVEMENT
    {
        if (isBackflipping || isWallJumping) //ignore user's joystick direction while backflipping
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
        CheckWall(); //checks for walls during movement lock.

        //WALL JUMP (only next to a wall and far from ground)
        if (isTouchingWall && jumped && !isNearGround)
        {
            jumped = true;
            clockoyote = 0f;

            movementBlocked = true;
            isWallJumping = true;
            movementBlockTimer = wallJumpLock;
            velocity = launchDirection * wallJumpHorizontal;
            velocity.y = wallJumpVertical;
            return;
        }

        //OTHER JUMPS ALLOWED DURING COYOTE TIME
        if (clockoyote > 0f) //preventing user from jumping more than once
        {

            //SPIN JUMP (only in ground and needs to be spinning)
            if (isSpinning && controller.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpSpinHeight * -2f * gravity);
                jumped = true;
            }

            //BASE JUMP
            else
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                clockoyote = 0f;
                jumped = true;
            }

            //BACKFLIP (needs to be in skid state)
            if (isSkidding)
            {
                isSkidding = false;
                speed = 0f;
                Backflip();

            }

            //variable reset after jumps
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
            if (moveInput.magnitude < 0.7f) //resets spin detection if rotation isn't fast enough
            {

                previousStick = Vector2.zero;
                rotationCheck = 0f;
                spinDirection = 0;
                isSpinning = false;
                return;
            }

            if (previousStick != Vector2.zero) //detects when player is moving joystick
            {
                float angleDelta = Vector2.SignedAngle(previousStick, moveInput); //storing spin angle

                if (Mathf.Abs(angleDelta) > 2f)
                {
                    spinCooldownTimer = spinTimeout;
                    int currentDirection = angleDelta > 0 ? 1 : -1;

                    if (spinDirection == 0) //detects beginning of spin
                    {
                        spinDirection = currentDirection;
                    }

                    if (currentDirection == spinDirection) //checks if the joystick goes to the same direction, if it does, add to the spin charge
                    {
                        rotationCheck += Mathf.Abs(angleDelta);
                    }
                    else //if not reset the charge
                    {
                        rotationCheck = 0f;
                        spinDirection = currentDirection;
                    }
                }
                spinCooldownTimer -= Time.deltaTime;

                if (spinCooldownTimer <= 0f) //will not trigger the spin if the spin action is too slow, prvents it from triggering during a normal turn
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


    void Backflip() //RESPONSIBLE OF THE BACKFLIP MECHANIC
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

            if (isBackflipping) //reset values after Backflip
            {
                isBackflipping = false;
                velocity.x = 0f;
                velocity.z = 0f;
                launchDirection = Vector3.zero;
            }
            if (isWallJumping) //reset values after Walljump
            {
                isWallJumping = false;

                speed = new Vector2(velocity.x, velocity.z).magnitude;
                moveDirection = new Vector3(velocity.x, 0f, velocity.z).normalized;
                lastMoveDirection = moveDirection;

                velocity.x = 0f;
                velocity.z = 0f;
                launchDirection = Vector3.zero;
            }

        }
    }


    void CheckWall() //RESPONSIBLE OF CHECKING IF THE PLAYER IS TOUCHING A WALL
    {
        isTouchingWall = false;

        Vector3 origin = transform.position + Vector3.up;

        if (Physics.Raycast(origin, modelTransform.forward, out RaycastHit hit, wallCheckDistance)) //the raycast checks if there is a wall
        {
            isTouchingWall = true;
            launchDirection = hit.normal;
        }
    }
}