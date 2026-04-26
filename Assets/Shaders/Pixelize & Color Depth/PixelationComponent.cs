using System;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/PixelationVolumeComponent")]
public class PixelationVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public ClampedFloatParameter intensity = new ClampedFloatParameter(value: 0, min: 0, max: 1, overrideState: true);
    public bool IsActive() => intensity.value > 0;
    public bool IsTileCompatible() => true;
}
