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

namespace Froststrap.Integrations
{
    public class StudioDiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient = new("1454451301130960896");
        private readonly ActivityWatcher _activityWatcher;
        private readonly Queue<StudioMessage> _messageQueue = new();

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private bool _visible = true;
        private bool _rpcEnabled = true;

        public StudioDiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence";

            _activityWatcher = activityWatcher;

            _activityWatcher.OnStudioRPCMessage += (_, message) => ProcessRPCMessage(message);
            _activityWatcher.OnStudioPlaceOpened += (_, _) => HandleStudioPlaceOpened();
            _activityWatcher.OnStudioPlaceClosed += (_, _) => HandleStudioPlaceClosed();

            _rpcClient.OnReady += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Received ready from user {e.User} ({e.User.ID})");

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            _rpcClient.Initialize();

            InitializeStudioPresence();
        }

        // for future use
        private void HandleStudioPlaceOpened()
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::HandleStudioPlaceOpened";
            App.Logger.WriteLine(LOG_IDENT, "Studio place opened");
        }

        private void HandleStudioPlaceClosed()
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::HandleStudioPlaceClosed";
            App.Logger.WriteLine(LOG_IDENT, "Studio place closed");

            ResetStudioPresence();
            _rpcEnabled = true;
        }

        public void ProcessRPCMessage(StudioMessage message, bool implicitUpdate = true)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::ProcessRPCMessage";

            if (message.StudioCommand == "RPCToggle")
            {
                var toggleData = message.Data.Deserialize<StudioToggleData>();
                if (toggleData != null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Processing RPCToggle: Enabled={toggleData.Enabled}");
                    HandleRPCToggle(toggleData.Enabled);
                }
                return;
            }
            else if (message.StudioCommand == "SetRichPresence")
            {
                if (!_rpcEnabled)
                {
                    App.Logger.WriteLine(LOG_IDENT, "RPC is disabled, ignoring SetRichPresence message");
                    return;
                }

                if (_currentPresence is null || _originalPresence is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Presence is not set, initializing Studio presence");
                    InitializeStudioPresence();
                }

                ProcessStudioRichPresence(message, implicitUpdate);

                if (implicitUpdate)
                    UpdatePresence();
            }
        }

        private void HandleRPCToggle(bool enabled)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::HandleRPCToggle";

            _rpcEnabled = enabled;
            App.Logger.WriteLine(LOG_IDENT, $"RPC is now {(enabled ? "ENABLED" : "DISABLED")}");

            if (enabled)
            {
                if (_originalPresence != null)
                {
                    _currentPresence = _originalPresence.Clone();
                    UpdatePresence();
                }
                else
                {
                    InitializeStudioPresence();
                }
            }
            else
            {
                ResetStudioPresence();
            }
        }

        private void InitializeStudioPresence()
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::InitializeStudioPresence", "Initializing Studio presence");

            _currentPresence = new DiscordRPC.RichPresence
            {
                Timestamps = new Timestamps { Start = DateTime.UtcNow },
                Assets = new Assets
                {
                    LargeImageKey = "roblox_studio",
                    LargeImageText = "Roblox Studio",
                },
            };

            _originalPresence = _currentPresence.Clone();

            while (_messageQueue.Any())
            {
                ProcessRPCMessage(_messageQueue.Dequeue(), false);
            }

            UpdatePresence();
        }

        private void ResetStudioPresence()
        {
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
                },
            };

            UpdatePresence();
        }

        private void ProcessStudioRichPresence(StudioMessage message, bool implicitUpdate)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::ProcessStudioRichPresence";
            StudioRichPresence? presenceData;

            Debug.Assert(_currentPresence is not null);
            Debug.Assert(_originalPresence is not null);

            try
            {
                presenceData = message.Data.Deserialize<StudioRichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse studio message!");
                return;
            }

            if (presenceData is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse studio message!");
                return;
            }

            if (!string.IsNullOrEmpty(presenceData.Details) && App.Settings.Prop.StudioWorkspaceInfo)
                _currentPresence.Details = presenceData.Details;

            if (!string.IsNullOrEmpty(presenceData.State) && App.Settings.Prop.StudioEditingInfo)
                _currentPresence.State = presenceData.State;

            string largeImageKey = "roblox_studio";
            string largeImageText = "Roblox Studio";

            if (!string.IsNullOrEmpty(presenceData.ScriptType) && App.Settings.Prop.StudioThumbnailChanging)
            {
                switch (presenceData.ScriptType.ToLower())
                {
                    case "server":
                        largeImageKey = "studio_server";
                        largeImageText = "Editing Server Script";
                        break;
                    case "client":
                        largeImageKey = "studio_client";
                        largeImageText = "Editing Client Script";
                        break;
                    case "server_module":
                    case "client_module":
                    case "module":
                        largeImageKey = "studio_module";
                        largeImageText = "Editing Module Script";
                        break;
                    case "developing":
                        largeImageKey = "roblox_studio";
                        largeImageText = "Roblox Studio";
                        break;
                }
            }

            string smallImageKey = null!;
            string smallImageText = null!;

            if (presenceData.Testing && App.Settings.Prop.StudioShowTesting)
            {
                smallImageKey = "play_icon";
                smallImageText = "Currently Testing";
            }

            _currentPresence.Assets = new Assets
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                SmallImageKey = smallImageKey,
                SmallImageText = smallImageText
            };

            if (_rpcEnabled)
            {
                _originalPresence = _currentPresence.Clone();
            }

            if (implicitUpdate)
                UpdatePresence();
        }

        public void SetVisibility(bool visible)
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

            _visible = visible;

            if (_visible)
                UpdatePresence();
            else
                _rpcClient.ClearPresence();
        }

        public void UpdatePresence()
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::UpdatePresence";

            if (_currentPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Presence is empty, clearing");
                _rpcClient.ClearPresence();
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Updating presence");

            if (_visible)
                _rpcClient.SetPresence(_currentPresence);
        }

        public void Dispose()
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::Dispose", "Cleaning up Discord RPC");
            _rpcClient.ClearPresence();
            _rpcClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}