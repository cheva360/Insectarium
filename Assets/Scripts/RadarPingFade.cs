using UnityEngine;

public class RadarPingFade : MonoBehaviour
{
    private Material pingMaterial;
    private float spawnTime;
    [SerializeField] private float fadeDuration = 2.0f;

    private void Start()
    {
        spawnTime = Time.time;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Unlit/Radar Fade"));
        renderer.material = mat;
        pingMaterial = renderer.material;
        pingMaterial.SetFloat("_FadeSpeed", 1.0f / fadeDuration);
        pingMaterial.enableInstancing = true;
    }

    private void Update()
    {

        float elapsedTime = Time.time - spawnTime;
            
        // Update shader with elapsed time
        //pingMaterial.SetFloat("_ElapsedTime", elapsedTime);

        // Optionally destroy after fade completes
        if (elapsedTime > fadeDuration)
        {
            Destroy(gameObject);
        }


        // Keep z rotation at 0 in world space to stay upright regardless of parent rotation
        Vector3 currentRotation = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
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