using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   Lists players and their gameplay attributes. Code is pretty much self-contained.
/// </summary>
public class ScoreBoard : VBoxContainer
{
    [Export]
    public NodePath ListPath = null!;

    [Export]
    public NodePath PlayerCountPath = null!;

    [Export]
    public PackedScene NetworkedPlayerLabelScene = null!;

    private Dictionary<int, NetPlayerLog> playerLogs = new();

    private VBoxContainer list = null!;
    private Label playerCount = null!;
    private KickPlayerDialog kickDialog = null!;

    public override void _Ready()
    {
        list = GetNode<VBoxContainer>(ListPath);
        playerCount = GetNode<Label>(PlayerCountPath);

        kickDialog = GetNode<KickPlayerDialog>("KickPlayerDialog");

        NetworkManager.Instance.Connect(
            nameof(NetworkManager.RegistrationToServerResultReceived), this, nameof(OnPlayerRegistered));

        GetTree().Connect("network_peer_disconnected", this, nameof(OnPlayerDisconnected));

        foreach (var player in NetworkManager.Instance.PlayerList)
        {
            RegisterPlayer(player.Key, player.Value.Name);
        }
    }

    public void SortHighestScoreFirst()
    {
        var ordered = NetworkManager.Instance.PlayerList
            .OrderByDescending(p =>
            {
                p.Value.Ints.TryGetValue("score", out int score);
                return score;
            })
            .Select(p => p.Key)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var log = playerLogs[ordered[i]];
            list.MoveChild(log, i);
        }
    }

    private void RegisterPlayer(int id, string name)
    {
        var label = (NetPlayerLog)NetworkedPlayerLabelScene.Instance();
        label.ID = id;
        label.PlayerName = name;

        label.Connect(nameof(NetPlayerLog.KickRequested), this, nameof(OnKickButtonPressed));

        list.AddChild(label);
        playerLogs.Add(id, label);

        playerCount.Text = $"{NetworkManager.Instance.PlayerList.Count}/" +
            $"{NetworkManager.Instance.Settings?.MaxPlayers}";
    }

    private void UnRegisterPlayer(int id)
    {
        if (playerLogs.TryGetValue(id, out NetPlayerLog label))
        {
            label.QueueFree();
            playerLogs.Remove(id);
        }
    }

    private void OnPlayerRegistered(int peerId, NetworkManager.RegistrationToServerResult result)
    {
        if (result == NetworkManager.RegistrationToServerResult.Success)
            RegisterPlayer(peerId, NetworkManager.Instance.GetPlayerInfo(peerId)!.Name);
    }

    private void OnPlayerDisconnected(int peerId)
    {
        UnRegisterPlayer(peerId);
    }

    private void OnKickButtonPressed(int peerId)
    {
        kickDialog.RequestKick(peerId);
    }
}
