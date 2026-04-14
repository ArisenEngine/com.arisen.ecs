using System;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A system that operates on Entities and Components.
/// </summary>
public interface ISystem
{
    string Name { get; }
    void Execute(EntityManager em, float dt);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ReadComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public ReadComponentAttribute(Type componentType) => ComponentType = componentType;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class WriteComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public WriteComponentAttribute(Type componentType) => ComponentType = componentType;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExecuteBeforeAttribute : Attribute
{
    public Type OtherSystemType { get; }
    public ExecuteBeforeAttribute(Type otherSystemType) => OtherSystemType = otherSystemType;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExecuteAfterAttribute : Attribute
{
    public Type OtherSystemType { get; }
    public ExecuteAfterAttribute(Type otherSystemType) => OtherSystemType = otherSystemType;
}
