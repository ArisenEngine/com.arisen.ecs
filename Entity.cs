using System;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A lightweight, blittable handle representing an Entity in the ECS.
/// This must remain a value type (struct) with no reference members to adhere to DOD principles.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Entity : IEquatable<Entity>
{
    public readonly int Id;
    public readonly int Generation;

    public Entity(int id, int generation)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Entity slot IDs cannot be negative.");
        }

        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Entity generations must be positive.");
        }

        Id = id;
        Generation = generation;
    }

    public static readonly Entity Null = default;

    public bool IsNull => Generation == 0;
    public bool IsValid => Id >= 0 && Generation > 0;

    public bool Equals(Entity other) => Id == other.Id && Generation == other.Generation;
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Id, Generation);
    public override string ToString() => IsNull ? "Entity.Null" : $"Entity({Id}:{Generation})";

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
