using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BlockieNPC : MonoBehaviour
{
    [Header("Velocidad")]
    public float baseSpeed = 2f;
    public float extraSpeedPerPlayer = 1f;
    public float turnSpeed = 360f;

    [Header("Peso")]
    public float mass = 200f;
    public float drag = 6f;
    public float angularDrag = 10f;

    [Header("Detección")]
    public float forwardCheck = 2.5f;
    public float sideCheck = 2f;
    public float rayHeight = 1f;
    public LayerMask obstacleMask;

    [Header("Jugador")]
    public string playerTag = "Player";

    private Rigidbody rb;
    private bool isTurning = false;
    private Quaternion targetRotation;
    private int playersPushing = 0;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = mass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        float currentSpeed = baseSpeed + (playersPushing * extraSpeedPerPlayer);
        playersPushing = 0;

        if (isTurning)
        {
            DoTurn();
        }
        else
        {
            CheckPathAndMove(currentSpeed);
        }
    }

    void CheckPathAndMove(float speed)
    {
        Vector3 origin = transform.position + Vector3.up * rayHeight;

        bool frontBlocked = Physics.Raycast(origin, transform.forward, forwardCheck, obstacleMask);
        bool leftBlocked = Physics.Raycast(origin, -transform.right, sideCheck, obstacleMask);
        bool rightBlocked = Physics.Raycast(origin, transform.right, sideCheck, obstacleMask);

        Debug.DrawRay(origin, transform.forward * forwardCheck, frontBlocked ? Color.red : Color.green);
        Debug.DrawRay(origin, -transform.right * sideCheck, leftBlocked ? Color.red : Color.yellow);
        Debug.DrawRay(origin, transform.right * sideCheck, rightBlocked ? Color.red : Color.blue);

        if (!frontBlocked)
        {
            Vector3 nextPos = rb.position + transform.forward * speed * Time.fixedDeltaTime;
            rb.MovePosition(nextPos);
            return;
        }

        if (!leftBlocked && !rightBlocked)
        {
            int randomSide = Random.Range(0, 2);

            if (randomSide == 0)
                StartTurn(-90f);
            else
                StartTurn(90f);
        }
        else if (!leftBlocked)
        {
            StartTurn(-90f);
        }
        else if (!rightBlocked)
        {
            StartTurn(90f);
        }
        else
        {
            StartTurn(180f);
        }
    }

    void StartTurn(float angle)
    {
        isTurning = true;
        targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y + angle, 0f);
    }

    void DoTurn()
    {
        Quaternion newRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(newRotation);

        if (Quaternion.Angle(rb.rotation, targetRotation) < 1f)
        {
            rb.MoveRotation(targetRotation);
            isTurning = false;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag))
            return;

        Vector3 toPlayer = (collision.transform.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, toPlayer);

        if (dot < -0.3f)
        {
            playersPushing++;
        }
    }
}