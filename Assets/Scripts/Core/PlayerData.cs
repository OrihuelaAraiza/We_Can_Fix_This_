using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "WCFT/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Movement")]
    public float moveForce = 30f;
    public float maxSpeed = 9f;
    public float rotationSpeed = 14f;
    public float jumpForce = 6f;
    public float groundDrag = 0f;
    public float airDrag = 0f;
    public float airControlMultiplier = 0.75f;

    [Header("Clumsy Physics")]
    public float wobbleTorque = 3f;
    public float mass = 1.2f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer;

    [Header("Visuals")]
    public Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
}
