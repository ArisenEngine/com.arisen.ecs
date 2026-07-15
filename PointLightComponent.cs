using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Scene-authored point light data. Position comes from TransformComponent.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PointLightComponent : IComponent
{
    public Vector3 Color;
    public float Intensity;
    public float Range;
    public byte Enabled;

    public bool IsEnabled => Enabled != 0 && Intensity > 0.0f && Range > 0.0f;

    public static PointLightComponent Default => new()
    {
        Color = Vector3.One,
        Intensity = 1.0f,
        Range = 4.0f,
        Enabled = 1
    };
}
