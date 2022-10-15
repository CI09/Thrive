using System.Text;
using Godot;

public class NetworkedPlayerLabel : PanelContainer
{
    [Export]
    public NodePath NamePath = null!;

    [Export]
    public NodePath KickButtonPath = null!;

    private CustomRichTextLabel? nameLabel;
    private Button? kickButton;

    private string playerName = string.Empty;
    private bool highlight;

    [Signal]
    public delegate void KickRequested(int id);

    public int ID { get; set; } = -1;

    public string PlayerName
    {
        get => playerName;
        set
        {
            playerName = value;

            if (nameLabel != null)
                UpdateName();
        }
    }

    public bool Highlight
    {
        get => highlight;
        set
        {
            if (highlight == value)
                return;

            highlight = value;
            UpdateReadyState();
        }
    }

    public override void _Ready()
    {
        nameLabel = GetNode<CustomRichTextLabel>(NamePath);
        kickButton = GetNode<Button>(KickButtonPath);

        NetworkManager.Instance.Connect(
            nameof(NetworkManager.PlayerStatusChanged), this, nameof(OnPlayerStatusChanged));

        UpdateName();
        UpdateKickButton();
        UpdateReadyState();
    }

    private void UpdateName()
    {
        if (nameLabel == null)
            throw new SceneTreeAttachRequired();

        var builder = new StringBuilder(50);

        builder.Append(PlayerName);

        if (ID == NetworkManager.DEFAULT_SERVER_ID)
        {
            builder.Append(' ');
            builder.Append("[color=#fe82ff][host][/color]");
        }

        var network = NetworkManager.Instance;

        var player = network.GetPlayerState(ID);
        if (player != null && player.Status != network.Player?.Status)
        {
            builder.Append(' ');
            builder.Append($"[{player.GetEnvironmentReadable()}]");
        }

        nameLabel.ExtendedBbcode = builder.ToString();
    }

    private void UpdateKickButton()
    {
        if (kickButton == null)
            throw new SceneTreeAttachRequired();

        kickButton.Visible = GetTree().IsNetworkServer() && ID != GetTree().GetNetworkUniqueId();
    }

    private void UpdateReadyState()
    {
        var stylebox = GetStylebox("panel").Duplicate(true) as StyleBoxFlat;
        stylebox!.BgColor = Highlight ? new Color(0.07f, 0.51f, 0.84f, 0.39f) : new Color(Colors.Black, 0.39f);
        AddStyleboxOverride("panel", stylebox);
    }

    private void OnPlayerStatusChanged(int peerId, NetPlayerStatus status)
    {
        _ = peerId;
        _ = status;
        UpdateName();
    }

    private void OnKickPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        EmitSignal(nameof(KickRequested), ID);
    }
}
