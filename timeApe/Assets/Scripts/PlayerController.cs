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

    [Header("Jumping")]
    public float jumpHeight = 10f;
    public float gravity = -90f;

    [Header("Camera")]
    public Transform cameraTransform; //The Camera pivot goes here in editor

    [Header("Spin")]
    public float jumpSpinHeight = 20f;
    public float spinTimeout = 0.20f;
    public float spinAngleTrigger = 720f;

    //private variables
    private bool walker;
    private float verticalLookRotation;
    private float speed;
    private CharacterController controller;
    private PlayerInput inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector2 previousStick = Vector2.zero;
    private bool isSpinning = false;
    private float rotationCheck = 0f;
    private int spinDirection = 0;
    private float spinCooldownTimer = 0f;




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
        inputActions.Player.Jump.performed += ctx => Jump();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        if (moveInput.magnitude > deadZone)
        {
            moveDirection = MoveDirect();
        }

        DetectSpin();
        Move();
        ApplyGravity();
        RotateCamera();
        RotateModel();
    }

    /*
     --------------------------------------- MAIN FUNCTIONS ----------------------------------------------------------------------------------
     */

    Vector3 MoveDirect()
    {
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


    void Move()
    {
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
        float midSpeed;
        //ifelse used to let keyboard users walk using shift.
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

        speed = Mathf.MoveTowards(speed, midSpeed, acceleration * Time.deltaTime);
        controller.Move(moveDirection * speed * Time.deltaTime); //actual moving registered
    }


    void ApplyGravity()
    {
        //this resets vertical speed when a player collides with a ceiling
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0)
        {
            velocity.y = 0f;
        }

        controller.Move(velocity * Time.deltaTime);
        if (controller.isGrounded && velocity.y < 0) //keeps player on the ground
        {
            velocity.y = -2f;
        }
        else    //applies gravity while middair
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    void Jump()
    {
        if (controller.isGrounded) //preventing user from jumping more than once
        {
            if (isSpinning)
            {
                velocity.y = Mathf.Sqrt(jumpSpinHeight * -2f * gravity);
            }

            else
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            //spin lock to ensure is always grounded
            isSpinning = false;
            rotationCheck = 0f;
            spinDirection = 0;
            previousStick = Vector2.zero;
        }
    }


    void RotateCamera()
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


    void RotateModel()
    {
        if (moveDirection.magnitude > 0.1f) //Moves only when joystick is tilted
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            modelTransform.rotation = Quaternion.Slerp(
                modelTransform.rotation,
                targetRotation,
                modelRotateSpeed * Time.deltaTime
            );
        }
    }


    void DetectSpin()
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

                //here
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
            Debug.Log(isSpinning); //ESTO ES DEBUG, QUITE ESTA LINEA SI HABLAS ESPANOL, VIVA COLOMBIA TRIPLE...
        }
    }
}
