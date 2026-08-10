using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Powerups;

public enum PowerupClientPhase : byte
{
    Cooldown,
    Ready,
    Active,
}

public static class PowerupRuntime
{
    private const string DebugPrefix = "[Powerups Debug]";
    private static PowerupRuntimeBehaviour behaviour;

    internal static void Log(string message)
    {
        Debug.Log($"{DebugPrefix} {message}");
    }

    internal static void Warn(string message)
    {
        Debug.LogWarning($"{DebugPrefix} {message}");
    }

    public static void EnsureCreated()
    {
        if (behaviour) return;

        GameObject runtimeObject = new GameObject("Powerups Client Runtime");
        UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
        behaviour = runtimeObject.AddComponent<PowerupRuntimeBehaviour>();
        Log("Created the persistent client runtime.");
    }

    public static void Shutdown()
    {
        if (!behaviour) return;

        behaviour.Teardown();
        UnityEngine.Object.Destroy(behaviour.gameObject);
        behaviour = null;
        Log("Destroyed the client runtime.");
    }

    public static void NotifyState(PowerupManager manager)
    {
        if (behaviour) behaviour.SendState(manager);
    }

    public static void NotifyPowerupStarted(PowerupManager manager)
    {
        if (!behaviour) return;

        behaviour.SendState(manager);
        behaviour.BroadcastVisual(manager.player.OwnerClientId, manager.activePowerup.name, true, manager.activePowerup.duration);
    }

    public static void NotifyPowerupEnded(PowerupManager manager, string powerupName)
    {
        if (!behaviour) return;

        behaviour.SendState(manager);
        behaviour.BroadcastVisual(manager.player.OwnerClientId, powerupName, false, 0f);
    }

    public static void NotifyLocalPlayerSpawned(Player player)
    {
        if (behaviour) behaviour.InitializeLocalPresentation(player);
    }

    public static void ObserveServerChat(string content)
    {
        if (behaviour) behaviour.ObserveServerChat(content);
    }
}

public sealed class PowerupRuntimeBehaviour : MonoBehaviour
{
    private const byte ProtocolVersion = 1;
    private const string HelloMessage = "wenright.powerups.hello.v1";
    private const string ActivateMessage = "wenright.powerups.activate.v1";
    private const string StateMessage = "wenright.powerups.state.v1";
    private const string VisualMessage = "wenright.powerups.visual.v1";

    private readonly HashSet<ulong> moddedClients = new HashSet<ulong>();
    private readonly Dictionary<ulong, PowerupVisualInstance> visuals = new Dictionary<ulong, PowerupVisualInstance>();
    private readonly Dictionary<ulong, string> lastLoggedServerStates = new Dictionary<ulong, string>();

    private NetworkManager registeredNetworkManager;
    private bool handlersRegistered;
    private bool helloAcknowledged;
    private float nextHelloAt;
    private float nextStateSyncAt;
    private int helloAttemptCount;
    private string lastNetworkSnapshot;
    private bool loggedMissingNetworkManager;
    private bool loggedMissingMessagingManager;
    private bool loggedMissingKeyboard;
    private bool loggedFirstInboundState;
    private bool loggedFirstInboundVisual;
    private bool clientPresentationInitialized;
    private bool loggedFirstHudDraw;
    private ulong localPresentationClientId = ulong.MaxValue;
    private float fallbackReadyAt;

    private PowerupClientPhase clientPhase = PowerupClientPhase.Cooldown;
    private string clientPowerupName = string.Empty;
    private float clientStateExpiresAt;
    private float clientPhaseDuration = 1f;

    private GUIStyle titleStyle;
    private GUIStyle detailStyle;
    private GUIStyle timerStyle;
    private Texture2D panelTexture;
    private Texture2D whiteTexture;

    private void Update()
    {
        BindToCurrentNetworkManager();

        NetworkManager networkManager = registeredNetworkManager;
        LogNetworkSnapshot(networkManager);

        bool fPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
        if (!networkManager || !networkManager.IsListening || !handlersRegistered)
        {
            if (fPressed)
            {
                PowerupRuntime.Warn($"F was pressed, but networking is not ready (manager={networkManager != null}, listening={networkManager && networkManager.IsListening}, handlers={handlersRegistered}).");
            }
            return;
        }

        if (networkManager.IsClient && networkManager.IsConnectedClient)
        {
            EnsureLocalPresentation();
            UpdatePresentationFallback();

            if (Keyboard.current == null)
            {
                if (!loggedMissingKeyboard)
                {
                    loggedMissingKeyboard = true;
                    PowerupRuntime.Warn("The connected client has no Keyboard.current device, so the F binding cannot be detected.");
                }
            }
            else
            {
                loggedMissingKeyboard = false;
            }

            if (!helloAcknowledged && Time.unscaledTime >= nextHelloAt)
            {
                SendHello();
                nextHelloAt = Time.unscaledTime + 2f;
            }

            if (fPressed)
            {
                if (CanReadActivationInput(out string rejectionReason))
                {
                    if (!helloAcknowledged)
                    {
                        PowerupRuntime.Warn($"F was pressed before state synchronization completed (hello attempts={helloAttemptCount}); sending the request anyway so the server can validate it.");
                    }
                    else if (clientPhase != PowerupClientPhase.Ready)
                    {
                        PowerupRuntime.Warn($"F was pressed while the cached client phase is {clientPhase}; sending the request anyway so the server can validate it.");
                    }
                    else
                    {
                        PowerupRuntime.Log($"F was pressed and passed all client gates (phase={clientPhase}, powerup='{clientPowerupName}').");
                    }
                    RequestActivation();
                }
                else
                {
                    PowerupRuntime.Warn($"F was pressed but activation was blocked: {rejectionReason}");
                }
            }
        }
        else if (fPressed)
        {
            PowerupRuntime.Warn($"F was pressed, but this NetworkManager is not a connected client (isClient={networkManager.IsClient}, connected={networkManager.IsConnectedClient}).");
        }

        if (networkManager.IsServer && Time.unscaledTime >= nextStateSyncAt)
        {
            nextStateSyncAt = Time.unscaledTime + 0.5f;
            SynchronizeClientStates();
        }
    }

    private void BindToCurrentNetworkManager()
    {
        NetworkManager current = NetworkManager.Singleton;
        if (registeredNetworkManager == current && handlersRegistered) return;
        if (!current && !registeredNetworkManager)
        {
            if (!loggedMissingNetworkManager)
            {
                loggedMissingNetworkManager = true;
                PowerupRuntime.Warn("NetworkManager.Singleton is not available yet; waiting for it.");
            }
            return;
        }

        UnregisterHandlers();
        registeredNetworkManager = current;
        helloAcknowledged = false;
        loggedFirstInboundState = false;
        loggedFirstInboundVisual = false;
        helloAttemptCount = 0;
        nextHelloAt = 0f;
        moddedClients.Clear();
        lastLoggedServerStates.Clear();
        ClearVisuals();

        if (!current)
        {
            if (!loggedMissingNetworkManager)
            {
                loggedMissingNetworkManager = true;
                PowerupRuntime.Warn("NetworkManager.Singleton is not available yet; waiting for it.");
            }
            return;
        }
        loggedMissingNetworkManager = false;
        if (current.CustomMessagingManager == null)
        {
            if (!loggedMissingMessagingManager)
            {
                loggedMissingMessagingManager = true;
                PowerupRuntime.Warn("NetworkManager exists, but CustomMessagingManager is null; handlers were not registered.");
            }
            return;
        }
        loggedMissingMessagingManager = false;

        CustomMessagingManager messaging = current.CustomMessagingManager;
        messaging.RegisterNamedMessageHandler(HelloMessage, HandleHello);
        messaging.RegisterNamedMessageHandler(ActivateMessage, HandleActivationRequest);
        messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
        messaging.RegisterNamedMessageHandler(VisualMessage, HandleVisual);
        handlersRegistered = true;
        PowerupRuntime.Log($"Registered powerup message handlers (isClient={current.IsClient}, isServer={current.IsServer}, listening={current.IsListening}, localClientId={current.LocalClientId}).");
    }

    private void UnregisterHandlers()
    {
        if (!handlersRegistered || !registeredNetworkManager || registeredNetworkManager.CustomMessagingManager == null)
        {
            handlersRegistered = false;
            return;
        }

        CustomMessagingManager messaging = registeredNetworkManager.CustomMessagingManager;
        messaging.UnregisterNamedMessageHandler(HelloMessage);
        messaging.UnregisterNamedMessageHandler(ActivateMessage);
        messaging.UnregisterNamedMessageHandler(StateMessage);
        messaging.UnregisterNamedMessageHandler(VisualMessage);
        handlersRegistered = false;
        PowerupRuntime.Log("Unregistered powerup message handlers.");
    }

    private void SendHello()
    {
        helloAttemptCount++;
        if (helloAttemptCount <= 3 || helloAttemptCount % 5 == 0)
        {
            PowerupRuntime.Log($"Sending client hello to server (attempt {helloAttemptCount}, localClientId={registeredNetworkManager.LocalClientId}).");
        }

        using FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp);
        writer.WriteByteSafe(ProtocolVersion);
        registeredNetworkManager.CustomMessagingManager.SendNamedMessage(
            HelloMessage,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);
    }

    private void HandleHello(ulong senderClientId, FastBufferReader reader)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsServer) return;
        if (!reader.TryBeginRead(1))
        {
            PowerupRuntime.Warn($"Server received an empty/malformed hello from client {senderClientId}.");
            return;
        }

        reader.ReadByteSafe(out byte version);
        if (version != ProtocolVersion)
        {
            PowerupRuntime.Warn($"Server rejected hello from client {senderClientId}: protocol {version}, expected {ProtocolVersion}.");
            return;
        }

        bool newlyDiscoveredClient = moddedClients.Add(senderClientId);
        if (newlyDiscoveredClient)
        {
            PowerupRuntime.Log($"Server accepted powerup client hello from client {senderClientId}. Known modded clients: {moddedClients.Count}.");
        }

        Player player = PlayerManager.Instance ? PlayerManager.Instance.GetPlayerByClientId(senderClientId) : null;
        if (player && PlayerBodyV2_Patch.powerupManagers.TryGetValue(player, out PowerupManager manager))
        {
            SendState(manager);
        }
        else if (newlyDiscoveredClient)
        {
            PowerupRuntime.Warn($"Hello from client {senderClientId} succeeded, but its Player/PowerupManager is not available yet; the periodic sync will retry.");
        }

        foreach (PowerupManager activeManager in PlayerBodyV2_Patch.powerupManagers.Values)
        {
            if (activeManager?.activePowerup == null || !activeManager.player) continue;

            float remaining = Mathf.Max(0f, activeManager.activePowerup.duration - (Time.time - activeManager.lastUsedAt));
            SendVisual(new[] { senderClientId }, activeManager.player.OwnerClientId, activeManager.activePowerup.name, true, remaining);
        }
    }

    private void RequestActivation()
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsClient || !registeredNetworkManager.IsConnectedClient)
        {
            PowerupRuntime.Warn("RequestActivation was called without a connected client NetworkManager.");
            return;
        }

        PowerupRuntime.Log($"Sending activation request to server for local client {registeredNetworkManager.LocalClientId}.");

        using FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp);
        writer.WriteByteSafe(ProtocolVersion);
        registeredNetworkManager.CustomMessagingManager.SendNamedMessage(
            ActivateMessage,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);
    }

    private void HandleActivationRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsServer) return;
        PowerupRuntime.Log($"Server received an activation request from client {senderClientId}.");
        if (!reader.TryBeginRead(1))
        {
            PowerupRuntime.Warn($"Server rejected activation from client {senderClientId}: malformed payload.");
            return;
        }

        reader.ReadByteSafe(out byte version);
        if (version != ProtocolVersion)
        {
            PowerupRuntime.Warn($"Server rejected activation from client {senderClientId}: protocol {version}, expected {ProtocolVersion}.");
            return;
        }

        moddedClients.Add(senderClientId);

        Player player = PlayerManager.Instance ? PlayerManager.Instance.GetPlayerByClientId(senderClientId) : null;
        if (!player)
        {
            PowerupRuntime.Warn($"Server rejected activation from client {senderClientId}: PlayerManager did not return a Player.");
            return;
        }
        if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(player, out PowerupManager manager))
        {
            PowerupRuntime.Warn($"Server rejected activation from client {senderClientId}: no PowerupManager exists for player '{player.Username.Value}'.");
            return;
        }

        if (!manager.CanUse() || manager.availablePowerup == null)
        {
            PowerupRuntime.Warn($"Server rejected activation from client {senderClientId}: canUse={manager.CanUse()}, available='{manager.availablePowerup?.name ?? "none"}', active='{manager.activePowerup?.name ?? "none"}', cooldownRemaining={Mathf.Max(0f, manager.nextPowerupAvailableAt - Time.time):0.00}s.");
            SendState(manager);
            return;
        }

        Powerup powerupUsed = manager.UsePowerup();
        if (powerupUsed == null)
        {
            PowerupRuntime.Warn($"Server's UsePowerup returned null for client {senderClientId}.");
            return;
        }

        PowerupRuntime.Log($"Server activated '{powerupUsed.name}' for client {senderClientId} / player '{player.Username.Value}'.");

        UIChatPatch.BroadcastNextFrame($"{player.Username.Value} used <b><color={powerupUsed.color}>{powerupUsed.name}</color></b>");
    }

    public void SendState(PowerupManager manager)
    {
        if (manager == null || !manager.player || !moddedClients.Contains(manager.player.OwnerClientId)) return;
        if (!registeredNetworkManager || !registeredNetworkManager.IsServer || registeredNetworkManager.CustomMessagingManager == null) return;

        GetManagerState(manager, out PowerupClientPhase phase, out string powerupName, out float remaining);

        string stateSignature = $"{phase}:{powerupName}";
        if (!lastLoggedServerStates.TryGetValue(manager.player.OwnerClientId, out string previousState) || previousState != stateSignature)
        {
            lastLoggedServerStates[manager.player.OwnerClientId] = stateSignature;
            PowerupRuntime.Log($"Server sending state to client {manager.player.OwnerClientId}: phase={phase}, powerup='{powerupName}', remaining={remaining:0.00}s.");
        }

        using FastBufferWriter writer = new FastBufferWriter(160, Allocator.Temp);
        writer.WriteByteSafe(ProtocolVersion);
        writer.WriteByteSafe((byte)phase);
        writer.WriteValueSafe(powerupName ?? string.Empty, true);
        writer.WriteValueSafe(remaining);

        registeredNetworkManager.CustomMessagingManager.SendNamedMessage(
            StateMessage,
            manager.player.OwnerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);
    }

    private static void GetManagerState(PowerupManager manager, out PowerupClientPhase phase, out string powerupName, out float remaining)
    {
        if (manager.activePowerup != null)
        {
            phase = PowerupClientPhase.Active;
            powerupName = manager.activePowerup.name;
            remaining = Mathf.Max(0f, manager.activePowerup.duration - (Time.time - manager.lastUsedAt));
            return;
        }

        if (manager.availablePowerup != null && manager.CanUse())
        {
            phase = PowerupClientPhase.Ready;
            powerupName = manager.availablePowerup.name;
            remaining = 0f;
            return;
        }

        phase = PowerupClientPhase.Cooldown;
        powerupName = manager.lastPowerupName ?? string.Empty;
        remaining = Mathf.Max(0f, manager.nextPowerupAvailableAt - Time.time);
    }

    private void SynchronizeClientStates()
    {
        if (registeredNetworkManager)
        {
            moddedClients.RemoveWhere(clientId => !registeredNetworkManager.ConnectedClients.ContainsKey(clientId));
        }

        foreach (PowerupManager manager in PlayerBodyV2_Patch.powerupManagers.Values)
        {
            SendState(manager);
        }
    }

    private void HandleState(ulong senderClientId, FastBufferReader reader)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsClient) return;

        if (!loggedFirstInboundState)
        {
            loggedFirstInboundState = true;
            PowerupRuntime.Log($"Client state handler received its first packet from sender {senderClientId} (static ServerClientId={NetworkManager.ServerClientId}, payloadLength={reader.Length}).");
        }

        reader.ReadByteSafe(out byte version);
        if (version != ProtocolVersion)
        {
            PowerupRuntime.Warn($"Client rejected state packet: protocol {version}, expected {ProtocolVersion}.");
            return;
        }

        reader.ReadByteSafe(out byte phaseValue);
        reader.ReadValueSafe(out string powerupName, true);
        reader.ReadValueSafe(out float remaining);

        PowerupClientPhase newPhase = (PowerupClientPhase)phaseValue;
        bool firstState = !helloAcknowledged;
        bool phaseChanged = newPhase != clientPhase || !string.Equals(powerupName, clientPowerupName, StringComparison.Ordinal);

        clientPhase = newPhase;
        clientPowerupName = powerupName ?? string.Empty;
        clientStateExpiresAt = Time.unscaledTime + Mathf.Max(0f, remaining);
        clientPhaseDuration = phaseChanged ? Mathf.Max(remaining, 0.01f) : Mathf.Max(clientPhaseDuration, remaining);
        helloAcknowledged = true;

        if (firstState || phaseChanged)
        {
            PowerupRuntime.Log($"Client received server state: phase={clientPhase}, powerup='{clientPowerupName}', remaining={remaining:0.00}s. Handshake acknowledged.");
        }
    }

    public void InitializeLocalPresentation(Player localPlayer)
    {
        if (!localPlayer) return;
        if (clientPresentationInitialized && localPresentationClientId == localPlayer.OwnerClientId) return;

        localPresentationClientId = localPlayer.OwnerClientId;
        clientPresentationInitialized = true;
        clientPhase = PowerupClientPhase.Cooldown;
        clientPowerupName = string.Empty;
        clientPhaseDuration = Mathf.Max(0.01f, Powerup.GetCooldown());
        clientStateExpiresAt = Time.unscaledTime + clientPhaseDuration;
        fallbackReadyAt = clientStateExpiresAt;
        PowerupRuntime.Log($"Initialized bottom-right HUD for local client {localPresentationClientId}; estimated initial cooldown={clientPhaseDuration:0.00}s.");
    }

    private void EnsureLocalPresentation()
    {
        if (clientPresentationInitialized || !PlayerManager.Instance) return;
        Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
        if (localPlayer) InitializeLocalPresentation(localPlayer);
    }

    private void UpdatePresentationFallback()
    {
        if (!clientPresentationInitialized || clientPhase != PowerupClientPhase.Active) return;
        if (Time.unscaledTime < clientStateExpiresAt) return;

        clientPhase = PowerupClientPhase.Cooldown;
        float remaining = Mathf.Max(0f, fallbackReadyAt - Time.unscaledTime);
        clientStateExpiresAt = Time.unscaledTime + remaining;
        clientPhaseDuration = Mathf.Max(0.01f, remaining);
        PowerupRuntime.Log($"Local presentation transitioned from Active to Cooldown; estimated remaining={remaining:0.00}s.");
    }

    public void ObserveServerChat(string content)
    {
        if (string.IsNullOrEmpty(content)) return;

        string powerupName = FindPowerupName(content);
        if (string.IsNullOrEmpty(powerupName)) return;

        const string usedMarker = " used <b><color=";
        int usedIndex = content.IndexOf(usedMarker, StringComparison.Ordinal);
        if (usedIndex > 0)
        {
            string username = content.Substring(0, usedIndex);
            ObservePowerupActivation(username, powerupName);
            return;
        }

        if (content.EndsWith("</color></b> is ready to use", StringComparison.Ordinal))
        {
            clientPresentationInitialized = true;
            clientPhase = PowerupClientPhase.Ready;
            clientPowerupName = powerupName;
            clientStateExpiresAt = 0f;
            clientPhaseDuration = 1f;
            PowerupRuntime.Log($"Chat fallback marked local powerup '{powerupName}' Ready.");
            return;
        }

        if (content.EndsWith("</color></b> ended", StringComparison.Ordinal) && clientPowerupName == powerupName)
        {
            clientPhase = PowerupClientPhase.Cooldown;
            float remaining = Mathf.Max(0f, fallbackReadyAt - Time.unscaledTime);
            clientStateExpiresAt = Time.unscaledTime + remaining;
            clientPhaseDuration = Mathf.Max(0.01f, remaining);
            PowerupRuntime.Log($"Chat fallback observed local powerup '{powerupName}' ending; cooldown remaining={remaining:0.00}s.");
        }
    }

    private void ObservePowerupActivation(string username, string powerupName)
    {
        Player activatedPlayer = FindPlayerByUsername(username);
        if (!activatedPlayer)
        {
            PowerupRuntime.Warn($"Chat fallback saw '{username}' use '{powerupName}', but could not resolve that Player for visuals.");
            return;
        }

        float duration = PowerupList.dict.TryGetValue(powerupName, out Powerup powerup) ? powerup.duration : 2f;
        StartVisual(activatedPlayer.OwnerClientId, powerupName, duration);
        PowerupRuntime.Log($"Chat fallback started '{powerupName}' visuals for client {activatedPlayer.OwnerClientId} / '{username}'.");

        NetworkManager networkManager = registeredNetworkManager;
        if (!networkManager || activatedPlayer.OwnerClientId != networkManager.LocalClientId) return;

        clientPresentationInitialized = true;
        clientPhase = PowerupClientPhase.Active;
        clientPowerupName = powerupName;
        clientPhaseDuration = Mathf.Max(0.01f, duration);
        clientStateExpiresAt = Time.unscaledTime + duration;
        float totalUntilReady = powerupName == PowerupNames.Backflip ? 3f : duration + Powerup.GetCooldown();
        fallbackReadyAt = Time.unscaledTime + totalUntilReady;
    }

    private static string FindPowerupName(string content)
    {
        foreach (string powerupName in PowerupList.dict.Keys)
        {
            if (content.IndexOf($">{powerupName}</color>", StringComparison.Ordinal) >= 0) return powerupName;
        }
        return null;
    }

    private static Player FindPlayerByUsername(string username)
    {
        if (!PlayerManager.Instance) return null;
        foreach (Player candidate in PlayerManager.Instance.GetPlayers(false))
        {
            if (candidate && string.Equals(candidate.Username.Value.ToString(), username, StringComparison.Ordinal)) return candidate;
        }
        return null;
    }

    public void BroadcastVisual(ulong ownerClientId, string powerupName, bool active, float duration)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsServer || moddedClients.Count == 0) return;

        SendVisual(new List<ulong>(moddedClients), ownerClientId, powerupName, active, duration);
    }

    private void SendVisual(IReadOnlyList<ulong> recipients, ulong ownerClientId, string powerupName, bool active, float duration)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsServer || recipients == null || recipients.Count == 0) return;

        using FastBufferWriter writer = new FastBufferWriter(176, Allocator.Temp);
        writer.WriteByteSafe(ProtocolVersion);
        writer.WriteValueSafe(active);
        writer.WriteValueSafe(ownerClientId);
        writer.WriteValueSafe(powerupName ?? string.Empty, true);
        writer.WriteValueSafe(duration);

        registeredNetworkManager.CustomMessagingManager.SendNamedMessage(
            VisualMessage,
            recipients,
            writer,
            NetworkDelivery.ReliableSequenced);
    }

    private void HandleVisual(ulong senderClientId, FastBufferReader reader)
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsClient) return;

        if (!loggedFirstInboundVisual)
        {
            loggedFirstInboundVisual = true;
            PowerupRuntime.Log($"Client visual handler received its first packet from sender {senderClientId} (static ServerClientId={NetworkManager.ServerClientId}, payloadLength={reader.Length}).");
        }

        reader.ReadByteSafe(out byte version);
        if (version != ProtocolVersion) return;

        reader.ReadValueSafe(out bool active);
        reader.ReadValueSafe(out ulong ownerClientId);
        reader.ReadValueSafe(out string powerupName, true);
        reader.ReadValueSafe(out float duration);

        if (active) StartVisual(ownerClientId, powerupName, duration);
        else StopVisual(ownerClientId);
    }

    private void StartVisual(ulong ownerClientId, string powerupName, float duration)
    {
        if (visuals.TryGetValue(ownerClientId, out PowerupVisualInstance existing) && existing)
        {
            if (existing.PowerupName == powerupName) return;
            Destroy(existing.gameObject);
        }
        visuals.Remove(ownerClientId);

        GameObject visualObject = new GameObject($"{powerupName} Visual ({ownerClientId})");
        PowerupVisualInstance visual = visualObject.AddComponent<PowerupVisualInstance>();
        visual.Initialize(ownerClientId, powerupName, duration);
        visuals[ownerClientId] = visual;
        PowerupRuntime.Log($"Created '{powerupName}' visual object for client {ownerClientId} (duration={duration:0.00}s).");
    }

    private void StopVisual(ulong ownerClientId)
    {
        if (visuals.TryGetValue(ownerClientId, out PowerupVisualInstance existing) && existing)
        {
            Destroy(existing.gameObject);
        }
        visuals.Remove(ownerClientId);
    }

    private bool CanReadActivationInput(out string rejectionReason)
    {
        if (Keyboard.current == null)
        {
            rejectionReason = "Unity Input System has no current Keyboard device";
            return false;
        }
        if (!PlayerManager.Instance)
        {
            rejectionReason = "PlayerManager.Instance is unavailable";
            return false;
        }
        if (!PlayerManager.Instance.GetLocalPlayer())
        {
            rejectionReason = "PlayerManager has no local Player";
            return false;
        }

        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!selected)
        {
            rejectionReason = null;
            return true;
        }

        if (selected.GetComponentInParent<TMP_InputField>() != null || selected.GetComponentInParent<InputField>() != null)
        {
            rejectionReason = $"an input field is focused ('{selected.name}')";
            return false;
        }

        rejectionReason = null;
        return true;
    }

    private void LogNetworkSnapshot(NetworkManager networkManager)
    {
        string snapshot = !networkManager
            ? "no NetworkManager"
            : $"manager={networkManager.name}, listening={networkManager.IsListening}, client={networkManager.IsClient}, connected={networkManager.IsConnectedClient}, server={networkManager.IsServer}, host={networkManager.IsHost}, localClientId={networkManager.LocalClientId}, handlers={handlersRegistered}";

        if (snapshot == lastNetworkSnapshot) return;
        lastNetworkSnapshot = snapshot;
        PowerupRuntime.Log($"Network state changed: {snapshot}.");
    }

    private void OnGUI()
    {
        if (!registeredNetworkManager || !registeredNetworkManager.IsClient || !registeredNetworkManager.IsConnectedClient) return;

        EnsureLocalPresentation();
        if (!clientPresentationInitialized) return;

        Player localPlayer = PlayerManager.Instance ? PlayerManager.Instance.GetLocalPlayer() : null;
        PlayerBody localPlayerBody = StickCompatibility.GetPlayerBody(localPlayer);
        if (!localPlayer || !localPlayer.IsCharacterSpawned || !localPlayerBody || !localPlayerBody.gameObject.activeInHierarchy) return;

        EnsureGuiAssets();

        if (!loggedFirstHudDraw)
        {
            loggedFirstHudDraw = true;
            PowerupRuntime.Log("Drawing the powerup HUD in the bottom-left corner while the local character is spawned.");
        }

        float width = 250f;
        float height = 88f;
        Rect panel = new Rect(0, Screen.height - height - 72f, width, height);
        GUI.DrawTexture(panel, panelTexture);

        Color accent = PowerupPalette.Get(clientPowerupName);
        bool showAbilityIcon = clientPhase != PowerupClientPhase.Cooldown && !string.IsNullOrEmpty(clientPowerupName);
        float textX = panel.x + 36f;
        if (showAbilityIcon)
        {
            Texture2D icon = PowerupIconFactory.Get(clientPowerupName);
            GUI.DrawTexture(new Rect(panel.x + 12f, panel.y + 15f, 58f, 58f), icon, ScaleMode.ScaleToFit, true);
            textX = panel.x + 82f;
        }

        string title = string.IsNullOrEmpty(clientPowerupName) ? "NEXT POWERUP" : clientPowerupName.ToUpperInvariant();
        if (clientPhase == PowerupClientPhase.Cooldown) title = "NEXT POWERUP";
        GUI.Label(new Rect(textX, panel.y + 13f, panel.xMax - textX - 12f, 24f), title, titleStyle);

        float remaining = Mathf.Max(0f, clientStateExpiresAt - Time.unscaledTime);
        string detail;
        if (clientPhase == PowerupClientPhase.Ready)
        {
            detail = "READY";
        }
        else if (clientPhase == PowerupClientPhase.Active)
        {
            detail = $"ACTIVE  {remaining:0.0}s";
        }
        else
        {
            detail = remaining > 0f ? $"COOLDOWN  {remaining:0.0}s" : "WAITING FOR POWERUP";
        }
        GUI.Label(new Rect(textX, panel.y + 38f, panel.xMax - textX - 12f, 22f), detail, clientPhase == PowerupClientPhase.Ready ? timerStyle : detailStyle);

        if (clientPhase != PowerupClientPhase.Ready)
        {
            Rect track = new Rect(textX, panel.y + 68f, panel.xMax - textX - 18f, 7f);
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(track, whiteTexture);
            GUI.color = accent;
            float fill = clientPhaseDuration <= 0f ? 0f : Mathf.Clamp01(remaining / clientPhaseDuration);
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height), whiteTexture);
            GUI.color = Color.white;
        }
    }

    private void EnsureGuiAssets()
    {
        if (panelTexture) return;

        panelTexture = MakeSolidTexture(new Color(0.035f, 0.045f, 0.06f, 0.92f));
        whiteTexture = MakeSolidTexture(Color.white);

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        detailStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.78f, 0.82f, 0.88f) },
        };
        timerStyle = new GUIStyle(detailStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.6f, 1f, 0.7f) },
        };
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void ClearVisuals()
    {
        foreach (PowerupVisualInstance visual in visuals.Values)
        {
            if (visual) Destroy(visual.gameObject);
        }
        visuals.Clear();
    }

    public void Teardown()
    {
        UnregisterHandlers();
        ClearVisuals();
        moddedClients.Clear();
    }

    private void OnDestroy()
    {
        Teardown();
    }
}
