using UnityEngine;

public enum CockroachColor {Brown, Green, Purple}

public class LargeCockroach : MonoBehaviour
{
    [Header("Cockroach Settings")]
    [SerializeField] private CockroachColor cockroachColor;


    [Header("Prefab Settings, Ignore")]
    [SerializeField] private Material brownMat;
    [SerializeField] private Material greenMat;
    [SerializeField] private Material purpleMat;

    [SerializeField] private Renderer[] renderers;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        switch (cockroachColor)
        {
            case CockroachColor.Brown:
                ChangeColor(brownMat);
                break;
            case CockroachColor.Green:
                ChangeColor(greenMat);
                break;
            case CockroachColor.Purple:
                ChangeColor(purpleMat);
                break;

        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

// method that changes the color of the mesh
    void ChangeColor(Material matColor)
    {
        foreach (Renderer i in renderers)
        {
            i.material = matColor;
        }
    }

}
