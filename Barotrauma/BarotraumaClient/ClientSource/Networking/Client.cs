﻿using Barotrauma.Sounds;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Networking
{
    partial class Client : IDisposable
    {
        public VoipSound VoipSound
        {
            get;
            set;
        }

        // Players can boost per-user volume by 200%
        public const float MaxVoiceChatBoost = 2.0f;

        private float voiceVolume = 1f;

        public float VoiceVolume
        {
            get => voiceVolume; 
            set => voiceVolume = Math.Clamp(value, 0f, MaxVoiceChatBoost);
        }

        private SoundChannel radioNoiseChannel;
        private float radioNoise;

        public float RadioNoise
        {
            get { return radioNoise; }
            set { radioNoise = MathHelper.Clamp(value, 0.0f, 1.0f); }
        }

        private bool mutedLocally;
        public bool MutedLocally
        {
            get { return mutedLocally; }
            set
            {
                if (mutedLocally == value) { return; }
                mutedLocally = value;
#if CLIENT
                GameMain.NetLobbyScreen.SetPlayerVoiceIconState(this, muted, mutedLocally);
                GameMain.GameSession?.CrewManager?.SetPlayerVoiceIconState(this, muted, mutedLocally);
#endif
            }
        }

        public bool IsOwner;


        public bool IsDownloading;

        public float Karma;

        public bool AllowKicking =>
            !IsOwner &&
            !HasPermission(ClientPermissions.Ban) &&
            !HasPermission(ClientPermissions.Kick) &&
            !HasPermission(ClientPermissions.Unban);

        public void UpdateVoipSound()
        {
            if (VoipSound == null || !VoipSound.IsPlaying)
            {
                radioNoiseChannel?.Dispose();
                radioNoiseChannel = null;
                if (VoipSound != null)
                {
                    DebugConsole.Log("Destroying voipsound");
                    VoipSound.Dispose();
                }
                VoipSound = null;
                return; 
            }

            if (Screen.Selected is ModDownloadScreen)
            {
                VoipSound.Gain = 0.0f;
            }
            
            float gain = 1.0f;
            float noiseGain = 0.0f;
            Vector3? position = null;
            if (character != null && !character.IsDead)
            {
                if (GameSettings.CurrentConfig.Audio.UseDirectionalVoiceChat)
                {
                    position = new Vector3(character.WorldPosition.X, character.WorldPosition.Y, 0.0f);
                }
                else
                {
                    float dist = Vector3.Distance(new Vector3(character.WorldPosition, 0.0f), GameMain.SoundManager.ListenerPosition);
                    gain = 1.0f - MathUtils.InverseLerp(VoipSound.Near, VoipSound.Far, dist);
                }
                if (!VoipSound.UsingRadio)
                {
                    //emulate the "garbling" of the text chat
                    //this in a sense means the volume diminishes exponentially when close to the maximum range of the sound
                    //(diminished by both the garbling and the distance attenuation)

                    //which is good, because we want the voice chat to become unintelligible close to the max range,
                    //and we need to heavily reduce the volume to do that (otherwise it's just quiet, but still intelligible)
                    float garbleAmount = ChatMessage.GetGarbleAmount(Character.Controlled, character, ChatMessage.SpeakRangeVOIP);
                    gain *= 1.0f - garbleAmount;
                }
                if (RadioNoise > 0.0f)
                {
                    noiseGain = gain * RadioNoise;
                    gain *= 1.0f - RadioNoise;
                }
            }
            VoipSound.SetPosition(position);
            VoipSound.Gain = gain;
            if (noiseGain > 0.0f)
            {
                if (radioNoiseChannel == null || !radioNoiseChannel.IsPlaying)
                {
                    radioNoiseChannel = SoundPlayer.PlaySound("radiostatic");
                    radioNoiseChannel.Category = SoundManager.SoundCategoryVoip;
                    radioNoiseChannel.Looping = true;
                }
                radioNoiseChannel.Near = VoipSound.Near;
                radioNoiseChannel.Far = VoipSound.Far;
                radioNoiseChannel.Position = position;
                radioNoiseChannel.Gain = noiseGain;
            }
            else if (radioNoiseChannel != null)
            {
                radioNoiseChannel.Gain = 0.0f;
            }
        }

        partial void InitProjSpecific()
        {
            VoipQueue = null; VoipSound = null;
            if (SessionId == GameMain.Client.SessionId) { return; }
            VoipQueue = new VoipQueue(SessionId, canSend: false, canReceive: true);
            GameMain.Client?.VoipClient?.RegisterQueue(VoipQueue);
            VoipSound = null;
        }

        public void SetPermissions(ClientPermissions permissions, IEnumerable<Identifier> permittedConsoleCommands)
        {
            List<DebugConsole.Command> permittedCommands = new List<DebugConsole.Command>();
            foreach (Identifier commandName in permittedConsoleCommands)
            {
                var consoleCommand = DebugConsole.Commands.Find(c => c.Names.Contains(commandName));
                if (consoleCommand != null)
                {
                    permittedCommands.Add(consoleCommand);
                }
            }
            SetPermissions(permissions, permittedCommands);
        }

        public void SetPermissions(ClientPermissions permissions, IEnumerable<DebugConsole.Command> permittedConsoleCommands)
        {
            if (GameMain.Client == null)
            {
                return;
            }
            Permissions = permissions;
            PermittedConsoleCommands.Clear();
            foreach (var command in permittedConsoleCommands)
            {
                PermittedConsoleCommands.Add(command);
            }
        }

        public void GivePermission(ClientPermissions permission)
        {
            if (GameMain.Client == null || !GameMain.Client.HasPermission(ClientPermissions.ManagePermissions))
            {
                return;
            }
            if (!Permissions.HasFlag(permission)) { Permissions |= permission; }
        }

        public void RemovePermission(ClientPermissions permission)
        {
            if (GameMain.Client == null || !GameMain.Client.HasPermission(ClientPermissions.ManagePermissions))
            {
                return;
            }
            if (Permissions.HasFlag(permission)) { Permissions &= ~permission; }
        }

        public bool HasPermission(ClientPermissions permission)
        {
            if (GameMain.Client == null)
            {
                return false;
            }

            return Permissions.HasFlag(permission);
        }

        public void ResetVotes()
        {
            for (int i = 0; i < votes.Length; i++)
            {
                votes[i] = null;
            }
        }

        partial void DisposeProjSpecific()
        {
            if (VoipQueue != null)
            {
                GameMain.Client.VoipClient.UnregisterQueue(VoipQueue);
            }
            if (VoipSound != null)
            {
                VoipSound.Dispose();
                VoipSound = null;
            }
            if (radioNoiseChannel != null)
            {
                radioNoiseChannel.Dispose();
                radioNoiseChannel = null;
            }
        }
    }
}
