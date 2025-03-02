﻿/// <summary>
///   Allows cell to store more stuff
/// </summary>
public class StorageComponent : EmptyOrganelleComponent
{
    public StorageComponent(float capacity)
    {
        Capacity = capacity;
    }

    public float Capacity { get; }
}

public class StorageComponentFactory : IOrganelleComponentFactory
{
    public float Capacity;

    public IOrganelleComponent Create()
    {
        return new StorageComponent(Capacity);
    }

    public void Check(string name)
    {
        if (Capacity <= 0.0f)
        {
            throw new InvalidRegistryDataException(name, GetType().Name,
                "Storage component capacity must be > 0.0f");
        }
    }
}

[JSONDynamicTypeAllowed]
public class StorageComponentUpgrades : IComponentSpecificUpgrades
{
    public StorageComponentUpgrades(Compound? specializedFor)
    {
        SpecializedFor = specializedFor;
    }

    public Compound? SpecializedFor { get; set; }

    public bool Equals(IComponentSpecificUpgrades other)
    {
        if (other is not StorageComponentUpgrades otherVacuole)
            return false;

        return SpecializedFor == otherVacuole.SpecializedFor;
    }

    public object Clone()
    {
        return new StorageComponentUpgrades(SpecializedFor);
    }
}
