using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using Unity.Collections;
using Unity.Netcode;

namespace POGLib.Core
{
    /// <summary>
    /// Host ⇄ client messages over NGO's CustomMessagingManager (named messages), because new RPCs are impossible
    /// under IL2CPP. A message is an action name and a text payload. Strings travel as a 4-byte length and UTF-8
    /// bytes written one byte at a time (the way POG Mod Check's lists travel, seen working over LAN 2026-11-19).
    /// Registered when a session starts (<see cref="Hook"/> from the StartHost/StartClient postfix), on whichever
    /// CustomMessagingManager that session has; a handler that throws is logged, never passed on.
    /// </summary>
    public sealed class HostBridge
    {
        internal HostBridge() { }

        internal const string MessageName = "POGLib.HostBridge";
        private const int MaxText = 64 * 1024;
        private readonly Dictionary<string, Action<ulong, string>> _handlers = new();
        private CustomMessagingManager _registeredOn;
        private CustomMessagingManager.HandleNamedMessageDelegate _handler;

        /// <summary>
        /// Handle <paramref name="action"/> when it arrives: the handler gets the sender's client id and the payload.
        /// Name actions after your mod (<c>"MyMod.open"</c>), so two mods never answer each other's; one handler per
        /// action, the last one registered wins. The library's own actions start with "shops.".
        /// </summary>
        public void RegisterHandler(string action, Action<ulong, string> handler)
        {
            if (!string.IsNullOrEmpty(action) && handler != null) _handlers[action] = handler;
        }

        /// <summary>Registers the message on the current session's manager (once per manager; safe without one).</summary>
        internal void Hook()
        {
            var cmm = NetworkManager.Singleton?.CustomMessagingManager;
            if (cmm == null || (_registeredOn != null && _registeredOn.Pointer == cmm.Pointer)) return;
            try
            {
                if (_handler == null) { Action<ulong, FastBufferReader> h = OnMessage; _handler = h; }
                cmm.RegisterNamedMessageHandler(MessageName, _handler);
                _registeredOn = cmm;
                MelonLogger.Msg("[POGLib] HostBridge hooked.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGLib] HostBridge could not register its message: {ex.Message}");
            }
        }

        /// <summary>The session ended: the next one has a new manager to register on.</summary>
        internal void Unhook()
        {
            try { _registeredOn?.UnregisterNamedMessageHandler(MessageName); } catch { }
            _registeredOn = null;
        }

        /// <summary>Client → host.</summary>
        public void SendToServer(string action, string payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.IsServer) return;
            Send(nm, NetworkManager.ServerClientId, action, payload);
        }

        /// <summary>Host → one client.</summary>
        public void SendToClient(ulong clientId, string action, string payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || clientId == NetworkManager.ServerClientId) return;
            Send(nm, clientId, action, payload);
        }

        private void Send(NetworkManager nm, ulong to, string action, string payload)
        {
            Hook();
            var a = Encoding.UTF8.GetBytes(action ?? "");
            var p = Encoding.UTF8.GetBytes(payload ?? "");
            var w = new FastBufferWriter(a.Length + p.Length + 8, Allocator.Temp);
            try
            {
                Write(w, a); Write(w, p);
                nm.CustomMessagingManager.SendNamedMessage(MessageName, to, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally { w.Dispose(); }
        }

        private static void Write(FastBufferWriter w, byte[] bytes)
        {
            for (int i = 0; i < 4; i++) w.WriteByteSafe((byte)(bytes.Length >> (8 * i)));
            foreach (var b in bytes) w.WriteByteSafe(b);
        }

        private static string Read(FastBufferReader r)
        {
            int length = 0;
            for (int i = 0; i < 4; i++) { r.ReadByteSafe(out byte b); length |= b << (8 * i); }
            if (length < 0 || length > MaxText || length > r.Length - r.Position) return null;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++) { r.ReadByteSafe(out byte b); bytes[i] = b; }
            return Encoding.UTF8.GetString(bytes);
        }

        private void OnMessage(ulong senderId, FastBufferReader reader)
        {
            string action = null;
            try
            {
                action = Read(reader);
                string payload = action == null ? null : Read(reader);
                if (action == null || payload == null) { MelonLogger.Warning($"[POGLib] HostBridge: an unreadable message from {senderId}."); return; }
                if (_handlers.TryGetValue(action, out var handler)) handler(senderId, payload);
                else MelonLogger.Warning($"[POGLib] HostBridge: no handler for '{action}'.");
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] HostBridge '{action}' from {senderId}: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
