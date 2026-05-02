using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private Transform MinimapRotate;
    [SerializeField] private GameObject RadarPingPrefab;
    [SerializeField] private GameObject RadarPingParent;
    [SerializeField] private float maxRadarDistance = 50f;
    [SerializeField] private AudioClip radarPingSound;
    [SerializeField] private float sweepWidth = 2f;      // Horizontal half-extent of the sweep wall
    [SerializeField] private float sweepHeight = 10f;    // Vertical half-extent of the sweep wall (tall wall)

    private float radarLineRotation = 0f;
    private float radarSweepAngle = 0f;
    private Dictionary<Collider, float> lastDetectionAngle = new Dictionary<Collider, float>();
    [SerializeField] private float redetectionAngle = 90f;
    private AudioSource audioSource;
    private bool soundPlayedThisFrame = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        soundPlayedThisFrame = false;

        radarLineRotation -= 30f * Time.deltaTime;
        radarSweepAngle += 30f * Time.deltaTime;

        if (radarSweepAngle >= 360f)
        {
            radarSweepAngle -= 360f;
            lastDetectionAngle.Clear();
        }

        RadarLine.transform.localRotation = Quaternion.Euler(
            RadarLine.transform.localEulerAngles.x,
            RadarLine.transform.localEulerAngles.y,
            radarLineRotation);

        PerformRadarSweep();

        MinimapRotate.transform.rotation = Quaternion.Euler(
            MinimapRotate.eulerAngles.x,
            MinimapRotate.eulerAngles.y,
            GameController.Instance.player.transform.eulerAngles.y);
    }

    private void PerformRadarSweep()
    {
        float absoluteAngle = radarSweepAngle + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;
        Quaternion sweepRotation = Quaternion.LookRotation(sweepDirection, Vector3.up);

        Vector3 origin = GameController.Instance.player.transform.position;

        // Half-extents: thin along sweep direction (0.1), wide in height (sweepHeight), wide sideways (sweepWidth)
        Vector3 halfExtents = new Vector3(sweepWidth, sweepHeight, 0.1f);

        RaycastHit[] hits = Physics.BoxCastAll(
            origin,
            halfExtents,
            sweepDirection,
            sweepRotation,
            maxRadarDistance
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("RadarObj"))
            {
                bool shouldDetect = false;

                if (!lastDetectionAngle.ContainsKey(hit.collider))
                {
                    shouldDetect = true;
                }
                else
                {
                    float angleDifference = Mathf.Abs(Mathf.DeltaAngle(lastDetectionAngle[hit.collider], radarSweepAngle));
                    if (angleDifference >= redetectionAngle)
                        shouldDetect = true;
                }

                if (shouldDetect)
                {
                    lastDetectionAngle[hit.collider] = radarSweepAngle;
                    CreateRadarPing(hit.collider.transform.position);
                }
            }
        }
    }

    private void CreateRadarPing(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(GameController.Instance.player.transform.position, worldPosition);
        Vector3 directionToObject = (worldPosition - GameController.Instance.player.transform.position).normalized;
        float angle = Vector3.SignedAngle(GameController.Instance.player.transform.forward, directionToObject, Vector3.up);

        float normalizedDistance = Mathf.Clamp01(distance / maxRadarDistance);
        float radarDistance = normalizedDistance * 0.45f;

        float angleRad = (angle + GameController.Instance.player.transform.eulerAngles.y) * Mathf.Deg2Rad;

        float radarX = radarDistance * Mathf.Sin(angleRad);
        float radarY = radarDistance * Mathf.Cos(angleRad);

        GameObject radarPing = Instantiate(RadarPingPrefab, RadarPingParent.transform);
        radarPing.transform.localPosition = new Vector3(radarX, -1.53f, -radarY);

        if (!soundPlayedThisFrame && radarPingSound != null)
        {
            audioSource.PlayOneShot(radarPingSound);
            soundPlayedThisFrame = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || GameController.Instance == null || GameController.Instance.player == null)
            return;

        Vector3 playerPos = GameController.Instance.player.transform.position;
        float absoluteAngle = radarSweepAngle + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(playerPos, playerPos + sweepDirection * maxRadarDistance);

        // Visualize the tall wall box at mid-range
        Gizmos.color = Color.blue;
        Gizmos.matrix = Matrix4x4.TRS(
            playerPos + sweepDirection * (maxRadarDistance * 0.5f),
            Quaternion.LookRotation(sweepDirection, Vector3.up),
            Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(sweepWidth * 2f, sweepHeight * 2f, 0.2f));
        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = Color.green;
        DrawCircle(playerPos, maxRadarDistance, 64);

        Gizmos.color = Color.yellow;
        Vector3 arcStart = Quaternion.Euler(0, GameController.Instance.player.transform.eulerAngles.y, 0) * Vector3.forward * maxRadarDistance;
        Gizmos.DrawLine(playerPos, playerPos + arcStart);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
