using UnityEngine;

public class RadarPingFade : MonoBehaviour
{
    private Material pingMaterial;
    private float spawnTime;
    [SerializeField] private float fadeDuration = 2.0f;
    [SerializeField] private Material radarPingMaterial;

    private void Start()
    {
        spawnTime = Time.time;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (radarPingMaterial != null)
        {
            pingMaterial = new Material(radarPingMaterial);
        }
        else
        {
            Debug.LogError("RadarPingFade: No material assigned!", this);
            return;
        }

        meshRenderer.material = pingMaterial;
        pingMaterial.SetFloat("_FadeSpeed", 1.0f / fadeDuration);
    }

    private void Update()
    {
        if (pingMaterial == null) return;

        float elapsedTime = Time.time - spawnTime;
        pingMaterial.SetFloat("_ElapsedTime", elapsedTime);

        if (elapsedTime > fadeDuration)
            Destroy(gameObject);

        // Keep z rotation at 0 in world space to stay upright regardless of parent rotation
        Vector3 currentRotation = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
    }

    private void OnDestroy()
    {
        if (pingMaterial != null)
            Destroy(pingMaterial);
    }
}