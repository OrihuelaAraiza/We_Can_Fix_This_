using UnityEngine;

public class Smoggos_NPC : NPCBehaviourBase
{
    public float speed = 3f;
    public float detectionDistance = 2f;

    public float radius = 10f;
    private Vector3 startPoint;

    public float zigzagAmount = 2f;     // qu� tanto se mueve a los lados
    public float zigzagSpeed = 2f;      // velocidad del zigzag

    void Start()
    {
        startPoint = transform.position;
    }

    void Update()
    {
        if (IsDisabled) return;

        // Movimiento hacia adelante
        Vector3 forwardMove = transform.forward * speed;

        // Movimiento lateral tipo zig-zag
        Vector3 zigzag = transform.right * Mathf.Sin(Time.time * zigzagSpeed) * zigzagAmount;

        // Combinar ambos movimientos
        transform.position += (forwardMove + zigzag) * Time.deltaTime;

        // Raycast para pared
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, detectionDistance))
        {
            GirarAleatorio();
        }

        // Limitar por radio
        float distance = Vector3.Distance(transform.position, startPoint);
        if (distance > radius)
        {
            Vector3 directionToCenter = (startPoint - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(directionToCenter),
                Time.deltaTime * 2f
            );
        }
    }

    void GirarAleatorio()
    {
        float randomTurn = Random.Range(-120f, 120f);
        transform.Rotate(0, randomTurn, 0);
    }
}