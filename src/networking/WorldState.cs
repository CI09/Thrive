using System.Collections.Generic;

/// <summary>
///   Stores serialized state snapshots of networked entities, ready to be sent over network.
/// </summary>
public class WorldState : INetworkSerializable
{
    public Dictionary<uint, PackedBytesBuffer> EntityStates { get; set; } = new();

    public void NetworkSerialize(PackedBytesBuffer buffer)
    {
        buffer.Write(EntityStates.Count);

        foreach (var snapshot in EntityStates)
        {
            buffer.Write(snapshot.Key);
            buffer.Write(snapshot.Value);
        }
    }

    public void NetworkDeserialize(PackedBytesBuffer buffer)
    {
        var snapshotCount = buffer.ReadInt32();

        for (int i = 0; i < snapshotCount; ++i)
        {
            var entityId = buffer.ReadUInt32();
            var snapshotData = buffer.ReadBuffer();
            EntityStates[entityId] = snapshotData;
        }
    }
}
