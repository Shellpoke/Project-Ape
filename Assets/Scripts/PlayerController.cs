using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public Transform modelTransform; //The Character Model Goes here in the editor
    public float slowSpeed = 1f;
    public float fastSpeed = 20f;
    public float acceleration = 2f;
    public float rotationSpeed = 10f;
    public float rotateSpeed = 10f;

    [Header("Jumping")]
    public float jumpHeight = 10f;
    public float gravity = -90f;

    [Header("Camera")]
    public Transform cameraTransform; //The Camera pivot goes here in editor

    //private variables
    private CharacterController controller;
    private PlayerInput inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private bool walker;
    private float verticalLookRotation;
    private Vector3 moveDirection;


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
        moveDirection = MoveDirect();
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
        return forward * moveInput.y + right * moveInput.x;
    }


    void Move()
    {
        float speed;
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);

        //ifelse used to let keyboard users walk using shift.
        if (walker)
        {
            speed = slowSpeed;
        }
        else
        {
            speed = Mathf.Lerp(slowSpeed, fastSpeed, inputMagnitude); //Lerp handles the speed scaling from the joystick
        }

        controller.Move(moveDirection * speed * Time.deltaTime); //actual moving registered
    }


    void ApplyGravity()
    {
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
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
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
                rotateSpeed * Time.deltaTime
            );
        }
    }
}
