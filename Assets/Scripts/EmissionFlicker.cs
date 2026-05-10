using UnityEngine;

public class EmissionFlicker : MonoBehaviour
{
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Color baseEmissionColor = Color.green;
    [SerializeField] [Range(1f, 5f)] private float brightnessMultiplier = 2f;
    [SerializeField] [Min(0.01f)] private float flickerInterval = 0.1f;

    private bool _bright = false;
    private float _timer = 0f;

    private void Start()
    {
        if (targetMaterial == null)
        {
            Debug.LogWarning("EmissionFlicker: No material assigned.");
            return;
        }

        targetMaterial.EnableKeyword("_EMISSION");
    }

    private void FixedUpdate()
    {
        if (targetMaterial == null) return;

        _timer += Time.fixedDeltaTime;

        if (_timer >= flickerInterval)
        {
            _timer -= flickerInterval;
            _bright = !_bright;

            Color emission = _bright
                ? baseEmissionColor * brightnessMultiplier
                : baseEmissionColor;

            targetMaterial.SetColor("_EmissionColor", emission);
        }
    }
}