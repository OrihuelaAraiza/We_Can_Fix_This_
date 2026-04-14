using System.Collections.Generic;
using UnityEngine;

public class CoopCamera : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float minZoom = 12f;
    [SerializeField] private float maxZoom = 25f;
    [SerializeField] private float zoomPadding = 4f;
    [SerializeField] private float heightMultiplier = 1.2f;
    [SerializeField] private float depthMultiplier = 0.8f;

    private PlayerManager playerManager;

    // ── Lifecycle ──────────────────────────────
    private void Start()
    {
        playerManager = FindObjectOfType<PlayerManager>();
    }

    private void LateUpdate()
    {
        if (playerManager == null || playerManager.PlayerCount == 0) return;

        Vector3 centroid = GetCentroid();
        float zoom = GetZoom();

        // Posición encima y detrás del centroid
        Vector3 targetPos = centroid + new Vector3(0f, zoom * heightMultiplier, -zoom * depthMultiplier);

        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        transform.LookAt(centroid + Vector3.up * 0.5f);
    }

    // ── Helpers ────────────────────────────────
    private Vector3 GetCentroid()
    {
        IReadOnlyList<PlayerMovement> players = playerManager.GetPlayers();
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var p in players)
        {
            if (p == null) continue;
            sum += p.transform.position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    private float GetZoom()
    {
        IReadOnlyList<PlayerMovement> players = playerManager.GetPlayers();
        if (players.Count <= 1) return minZoom;

        Vector3 centroid = GetCentroid();
        float maxDist = 0f;
        foreach (var p in players)
        {
            if (p == null) continue;
            float dist = Vector3.Distance(p.transform.position, centroid);
            if (dist > maxDist) maxDist = dist;
        }
        return Mathf.Clamp(maxDist + zoomPadding, minZoom, maxZoom);
    }

    // ── Gizmos ─────────────────────────────────
    private void OnDrawGizmos()
    {
        if (playerManager == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetCentroid(), 0.5f);
    }
}