using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "WCFT/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Movement")]
    public float moveForce = 18f;
    public float maxSpeed = 7f;
    public float rotationSpeed = 12f;
    public float jumpForce = 6f;
    public float groundDrag = 6f;
    public float airDrag = 0.5f;
    public float airControlMultiplier = 0.4f;

    [Header("Clumsy Physics")]
    public float wobbleTorque = 3f;
    public float mass = 1.5f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer;

    [Header("Visuals")]
    public Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
}
