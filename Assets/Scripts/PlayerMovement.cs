using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    //Variables

    private CharacterController controller;

    public float speed = 12f;
    public float gravity = -9.8f * 2;
    public float jumpHeight = 3f;

    //Checks if we are standing on ground when jump, tf no infinite jump(Maybe we add double jump??)
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    Vector3 velocity;

    bool isGrounded;
    bool isMoving;

    private Vector3 lastPosition = new Vector3(0f, 0f, 0f);





    void Start()
    {
        // initialization code

        controller = GetComponent<CharacterController>();


    }

    void Update()
    {
        // movement logic

        //Ground check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        //Reset the default velocity
        if(isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        //Getting the inputs
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        //Creating the moving vector
        Vector3 move = transform.right * x + transform.forward * z;//(Right - red axis, forward - blue axis)

        //Actually moving
        controller.Move(move * speed * Time.deltaTime);

        // Check if the player can jump 

        if(Input.GetButton("Jump") && isGrounded)
        {
            //Actually jumping
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        //Falling down
        velocity.y += gravity * Time.deltaTime;

        //Executing the jump
        controller.Move(velocity * Time.deltaTime);

        if(lastPosition != gameObject.transform.position && isGrounded == true)
        { 
            isMoving = true;
            //For later use

        
        }
        else
        {
            isMoving = false;
            //For later use 
        }

        lastPosition = gameObject.transform.position;
        
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
