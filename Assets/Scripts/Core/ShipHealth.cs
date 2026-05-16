using System;
using System.Collections.Generic;
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

    public static event Action<float> OnHealthChanged;
    public static event Action        OnShipDestroyed;
    public static event Action        OnShipCritical;
    public static event Action        OnShipRecovered;

    bool isCritical;
    bool destroyedRaised;
    readonly List<RepairStation> stations = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    void OnEnable()
    {
        RepairStation.OnRegistered += RegisterStation;
        RepairStation.OnUnregistered += UnregisterStation;

        stations.Clear();
        foreach (RepairStation station in RepairStation.ActiveStations)
            RegisterStation(station);
    }

    void OnDisable()
    {
        RepairStation.OnRegistered -= RegisterStation;
        RepairStation.OnUnregistered -= UnregisterStation;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        TickHealth(Time.deltaTime);
    }

    internal void TickHealth(float deltaTime)
    {
        if (currentHealth <= 0) return;

        int brokenCount = CountBrokenStations();

        if (brokenCount > 0)
        {
            ApplyHealthDelta(-brokenCount * drainPerBrokenStation * deltaTime);
        }
        else if (currentHealth < maxHealth)
        {
            ApplyHealthDelta(regenPerSecond * deltaTime);
        }
    }

    int CountBrokenStations()
    {
        int count = 0;
        for (int i = stations.Count - 1; i >= 0; i--)
        {
            RepairStation s = stations[i];
            if (s == null)
            {
                stations.RemoveAt(i);
                continue;
            }

            if (s.State == RepairStation.StationState.Broken)
                count++;
        }

        return count;
    }

    public void ApplyDamage(float amount)
    {
        if (!IsAlive) return;
        ApplyHealthDelta(-amount);
    }

    public void Repair(float amount)
    {
        if (!IsAlive) return;
        ApplyHealthDelta(amount);
    }

    void RegisterStation(RepairStation station)
    {
        if (station != null && !stations.Contains(station))
            stations.Add(station);
    }

    void UnregisterStation(RepairStation station)
    {
        if (station != null)
            stations.Remove(station);
    }

    void ApplyHealthDelta(float delta)
    {
        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + delta, 0f, maxHealth);

        if (Mathf.Approximately(previousHealth, currentHealth))
            return;

        OnHealthChanged?.Invoke(HealthPercent);
        EvaluateHealthState();
    }

    void EvaluateHealthState()
    {
        if (currentHealth <= 0f)
        {
            if (destroyedRaised)
                return;

            destroyedRaised = true;
            OnShipDestroyed?.Invoke();
            Debug.Log("[ShipHealth] SHIP DESTROYED");
            return;
        }

        if (!isCritical && HealthPercent <= criticalThreshold)
        {
            isCritical = true;
            OnShipCritical?.Invoke();
            Debug.Log("[ShipHealth] CRITICAL STATE");
        }
        else if (isCritical && HealthPercent > criticalThreshold)
        {
            isCritical = false;
            OnShipRecovered?.Invoke();
            Debug.Log("[ShipHealth] Ship recovered");
        }
    }
}
