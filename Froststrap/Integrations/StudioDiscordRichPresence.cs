/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using DiscordRPC;
using System.Net.Sockets;

namespace Froststrap.Integrations
{
    public class StudioDiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient? _rpcClient;
        private readonly ActivityWatcher _activityWatcher;
        private readonly Queue<StudioMessage> _messageQueue = [];
        private readonly bool _isMacOS;

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private bool _visible = true;
        private bool _rpcEnabled = true;
        private bool _disposed = false;

        public StudioDiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence";

            _isMacOS = OperatingSystem.IsMacOS();

            if (_isMacOS)
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping Discord RPC initialization on macOS");
                _rpcClient = null!;
                _activityWatcher = activityWatcher;
                return;
            }

            _rpcClient = new DiscordRpcClient("1454451301130960896");
            _activityWatcher = activityWatcher;

            _activityWatcher.OnStudioRPCMessage += (_, message) => ProcessRPCMessage(message);
            _activityWatcher.OnStudioPlaceOpened += (_, _) => HandleStudioPlaceOpened();
            _activityWatcher.OnStudioPlaceClosed += (_, _) => HandleStudioPlaceClosed();

            _rpcClient.OnReady += (_, e) =>
            {
                App.Logger.WriteLine(LOG_IDENT, $"Received ready from user {e.User} ({e.User.ID})");
                if (!_disposed) InitializeStudioPresence();
            };

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            try
            {
                _rpcClient.Initialize();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to init RPC: {ex.Message}");
            }
        }

        // for future use
        private static void HandleStudioPlaceOpened()
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::HandleStudioPlaceOpened";
            App.Logger.WriteLine(LOG_IDENT, "Studio place opened");
        }

        private void HandleStudioPlaceClosed()
        {
            if (_isMacOS || _disposed) return;

            const string LOG_IDENT = "StudioDiscordRichPresence::HandleStudioPlaceClosed";
            App.Logger.WriteLine(LOG_IDENT, "Studio place closed");

            ResetStudioPresence();
            _rpcEnabled = true;
        }

        public void ProcessRPCMessage(StudioMessage message, bool implicitUpdate = true)
        {
            if (_isMacOS || _disposed) return;

            if (message.StudioCommand == "SetRichPresence")
            {
                if (!_rpcEnabled) return;

                if (_currentPresence is null || _originalPresence is null)
                    InitializeStudioPresence();

                ProcessStudioRichPresence(message, implicitUpdate);
            }
        }

        private void InitializeStudioPresence()
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            App.Logger.WriteLine("StudioDiscordRichPresence::InitializeStudioPresence", "Initializing Studio presence");

            _currentPresence = new DiscordRPC.RichPresence
            {
                Timestamps = new Timestamps { Start = DateTime.UtcNow },
                Assets = new Assets
                {
                    LargeImageKey = "roblox_studio",
                    LargeImageText = "Roblox Studio",
                    SmallImageKey = null,
                    SmallImageText = null
                },
            };

            _originalPresence = _currentPresence.Clone();

            while (_messageQueue.Count > 0)
            {
                ProcessRPCMessage(_messageQueue.Dequeue(), false);
            }

            UpdatePresence();
        }

        private void ResetStudioPresence()
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            App.Logger.WriteLine("StudioDiscordRichPresence::ResetStudioPresence", "Resetting Studio presence");

            DateTime? existingTimestamp = _currentPresence?.Timestamps?.Start;

            _currentPresence = new DiscordRPC.RichPresence
            {
                Timestamps = existingTimestamp.HasValue
                    ? new Timestamps { Start = existingTimestamp.Value }
                    : new Timestamps { Start = DateTime.UtcNow },
                Assets = new Assets
                {
                    LargeImageKey = "roblox_studio",
                    LargeImageText = "Roblox Studio",
                    SmallImageKey = null,
                    SmallImageText = null
                },
            };

            UpdatePresence();
        }

        private void ProcessStudioRichPresence(StudioMessage message, bool implicitUpdate)
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            const string LOG_IDENT = "StudioDiscordRichPresence::ProcessStudioRichPresence";
            StudioRichPresence? presenceData;

            try
            {
                presenceData = message.Data.Deserialize<StudioRichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse studio message!");
                return;
            }

            if (presenceData is null) return;

            if (!string.IsNullOrEmpty(presenceData.Details) && App.Settings.Prop.StudioWorkspaceInfo)
            {
                _currentPresence!.Details = presenceData.DevCount > 1
                    ? $"{presenceData.Details} ({presenceData.DevCount} Developers)"
                    : presenceData.Details;
            }

            if (!string.IsNullOrEmpty(presenceData.State) && App.Settings.Prop.StudioEditingInfo)
                _currentPresence!.State = presenceData.State;

            string largeImageKey = "roblox_studio";
            string largeImageText = "Roblox Studio";

            if (!string.IsNullOrEmpty(presenceData.ScriptType) && App.Settings.Prop.StudioThumbnailChanging)
            {
                largeImageKey = presenceData.ScriptType.ToLower() switch
                {
                    "server script" => "studio_server",
                    "local script" => "studio_client",
                    "module" or "server module" or "client module" => "studio_module",
                    _ => "roblox_studio"
                };

                largeImageText = $"Editing {presenceData.ScriptType}";
            }

            string? smallImageKey = null;
            if (presenceData.Testing && App.Settings.Prop.StudioShowTesting)
                smallImageKey = "play_icon";

            _currentPresence!.Assets = new Assets
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                SmallImageKey = smallImageKey ?? string.Empty,
                SmallImageText = presenceData.Testing && App.Settings.Prop.StudioShowTesting ? "Currently Testing" : null
            };

            if (App.Settings.Prop.StudioGameButton && presenceData.PlaceId > 0 && presenceData.IsPublic)
                _currentPresence.Buttons = [new Button { Label = "Open Roblox Game", Url = $"https://www.roblox.com/games/{presenceData.PlaceId}" }];
            else
                _currentPresence.Buttons = null;

            if (_rpcEnabled) _originalPresence = _currentPresence.Clone();

            if (implicitUpdate)
                UpdatePresence();
        }

        public void SetVisibility(bool visible)
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            App.Logger.WriteLine("StudioDiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

            _visible = visible;

            if (_visible)
                UpdatePresence();
            else
                _rpcClient.ClearPresence();
        }

        public void UpdatePresence()
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            const string LOG_IDENT = "StudioDiscordRichPresence::UpdatePresence";

            if (_currentPresence is null || !_rpcClient.IsInitialized)
                return;

            try
            {
                if (_visible)
                    _rpcClient.SetPresence(_currentPresence);
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Socket interrupted on macOS. Suppressed.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            App.Logger.WriteLine("StudioDiscordRichPresence::Dispose", "Cleaning up Discord RPC");

            if (_rpcClient != null)
            {
                try
                {
                    if (_rpcClient.IsInitialized)
                    {
                        try { _rpcClient.ClearPresence(); } catch (IOException) { }
                    }
                    _rpcClient.Dispose();
                }
                catch (IOException ex) when (ex.InnerException is SocketException) { }
                catch (Exception ex) { App.Logger.WriteException("StudioDiscordRichPresence::Dispose", ex); }
            }

            GC.SuppressFinalize(this);
        }
    }
}