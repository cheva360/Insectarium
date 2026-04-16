using UnityEngine;

public class RadarPingFade : MonoBehaviour
{
    private Material pingMaterial;
    private float spawnTime;
    [SerializeField] private float fadeDuration = 2.0f;

    private void Start()
    {
        spawnTime = Time.time;
        pingMaterial = GetComponent<MeshRenderer>().material;
    }

    private void Update()
    {
        if (pingMaterial != null)
        {
            float elapsedTime = Time.time - spawnTime;
            
            // Update shader with elapsed time
            pingMaterial.SetFloat("_ElapsedTime", elapsedTime);
            
            // Optionally destroy after fade completes
            if (elapsedTime > fadeDuration)
            {
                Destroy(gameObject);
            }
        }
    }
    
    private void OnDestroy()
    {
        // Clean up material instance
        if (pingMaterial != null)
        {
            Destroy(pingMaterial);
        }
    }
}