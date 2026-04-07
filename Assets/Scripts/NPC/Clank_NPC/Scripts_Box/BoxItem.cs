using UnityEngine;

public class BoxItem : MonoBehaviour
{
    public bool delivered = false;
    public bool pickedUp = false;

    //Bloqueo
    public bool blocked = false;

    // Cooldown 
    private float cooldownTimer = 0f;

    public Transform pickUpSnap;

    void Awake()
    {
        if (pickUpSnap == null)
        {
            Transform t = transform.Find("PickUpSnap");
            if (t != null) pickUpSnap = t;
        }
    }

    void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    // 5 min de cooldown 
    public void StartCooldown(float time = 300f)
    {
        cooldownTimer = time;
    }

    public bool IsOnCooldown()
    {
        return cooldownTimer > 0f;
    }
}