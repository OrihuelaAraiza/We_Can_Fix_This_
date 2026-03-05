using System;
using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    public static ShipHealth Instance { get; private set; }

    [Header("Config")]
    [SerializeField] float maxHealth             = 100f;
    [SerializeField] float drainPerBrokenStation = 3f;
    [SerializeField] float regenPerSecond        = 1f;
    [SerializeField] float criticalThreshold     = 0.3f;

    [Header("Runtime")]
    [SerializeField] float currentHealth = 100f;

    public float CurrentHealth  => currentHealth;
    public float MaxHealth      => maxHealth;
    public float HealthPercent  => currentHealth / maxHealth;
    public bool  IsAlive        => currentHealth > 0f;

    public event Action<float> OnHealthChanged;
    public event Action        OnShipDestroyed;
    public event Action        OnShipCritical;
    public static event Action OnShipRecovered;

    bool isCritical;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (currentHealth <= 0) return;

        // Contar estaciones rotas activamente
        int brokenCount = CountBrokenStations();

        if (brokenCount > 0)
        {
            // Drenar según cuántas estaciones están rotas
            float drain = brokenCount * drainPerBrokenStation * Time.deltaTime;
            currentHealth -= drain;
            currentHealth = Mathf.Max(0, currentHealth);
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            if (currentHealth <= 0)
            {
                OnShipDestroyed?.Invoke();
                Debug.Log("[ShipHealth] NAVE DESTRUIDA");
            }
            else if (!isCritical && currentHealth / maxHealth <= criticalThreshold)
            {
                isCritical = true;
                OnShipCritical?.Invoke();
                Debug.Log("[ShipHealth] ESTADO CRÍTICO");
            }
        }
        else if (currentHealth < maxHealth)
        {
            // Regenerar lentamente cuando todo está OK
            currentHealth += regenPerSecond * Time.deltaTime;
            currentHealth = Mathf.Min(maxHealth, currentHealth);
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            if (isCritical && currentHealth / maxHealth > criticalThreshold)
            {
                isCritical = false;
                OnShipRecovered?.Invoke();
                Debug.Log("[ShipHealth] Nave recuperada");
            }
        }
    }

    int CountBrokenStations()
    {
        var stations = FindObjectsOfType<RepairStation>();
        int count = 0;
        foreach (var s in stations)
            if (s.State == RepairStation.StationState.Broken)
                count++;
        return count;
    }

    public void ApplyDamage(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(HealthPercent);
        if (currentHealth <= 0f)
        {
            OnShipDestroyed?.Invoke();
            Debug.Log("[ShipHealth] Ship destroyed - GAME OVER");
        }
    }

    public void Repair(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(HealthPercent);
    }
}
