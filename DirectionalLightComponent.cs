using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Scene-authored directional light data. Direction points from the shaded point toward the light.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DirectionalLightComponent : IComponent
{
    public Vector3 Direction;
    public Vector3 Color;
    public float Intensity;
    public float AmbientIntensity;
    public byte Enabled;

    public bool IsEnabled => Enabled != 0 && Intensity > 0.0f;

    public static DirectionalLightComponent Default => new()
    {
        Direction = Vector3.Normalize(new Vector3(0.35f, 0.65f, -0.68f)),
        Color = Vector3.One,
        Intensity = 1.0f,
        AmbientIntensity = 0.18f,
        Enabled = 1
    };
}
