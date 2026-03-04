using System;
using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    public static ShipHealth Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float drainPerSecond = 2f;
    [SerializeField] private bool drainingActive = true;

    [Header("Runtime")]
    [SerializeField] private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;
    public bool IsAlive => currentHealth > 0f;

    public event Action<float> OnHealthChanged;   // param: 0-1
    public event Action        OnShipDestroyed;
    public event Action        OnShipCritical;    // fired once at 30%

    private bool criticalFired;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!drainingActive || !IsAlive) return;
        ApplyDamage(drainPerSecond * Time.deltaTime);
    }

    public void ApplyDamage(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(HealthPercent);

        if (!criticalFired && currentHealth <= maxHealth * 0.3f)
        {
            criticalFired = true;
            OnShipCritical?.Invoke();
            Debug.Log("[ShipHealth] CRITICAL! Ship below 30%");
        }

        if (currentHealth <= 0f)
        {
            OnShipDestroyed?.Invoke();
            Debug.Log("[ShipHealth] Ship destroyed - GAME OVER");
        }
    }

    public void Repair(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        if (currentHealth > maxHealth * 0.3f) criticalFired = false;
        OnHealthChanged?.Invoke(HealthPercent);
    }

    public void SetDraining(bool active) => drainingActive = active;
}
