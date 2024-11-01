using System;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("NetHawk", "jitcoder", "0.0.1")]
    [Description("Integrates with NetHawk WAF & DDOS Rust extension, provides insights into Rust player activities to provide additional DDOS protections")]
    public class NetHawk : RustPlugin
    {

        private class Settings
        {
            [JsonProperty(PropertyName="host")]
            public string Host { get; set; }
            [JsonProperty(PropertyName="username")]
            public string Username { get; set; }
            [JsonProperty(PropertyName="password")]
            public string Password { get; set; }
            [JsonProperty(PropertyName ="ip_whitelist")]
            public List<string> Whitelist { get; set; }
            [JsonProperty(PropertyName = "client_command_filters")]
            public List<string> ClientCommandFilters { get; set; }

        }
        private string? serverName;
        private string? url;
        private Dictionary<string, string>? headers;
        private Settings settings;

        private void Init()
        {
            serverName = ConVar.Server.hostname;
            LoadConfig();
            Puts("Nethawk Configuration Loaded:");
            Puts($"config.host = {settings.Host}");
            Puts($"config.username = {settings.Username}");

            Puts($"config.ClientCommandFilters: ");
            foreach (var filter in settings.ClientCommandFilters)
            {
                Puts($"filter client command {filter}");
            }

            Puts($"config.Whitelist: ");
            foreach (var ip in settings.Whitelist)
            {
                Puts($"ip whitelist {ip}");
            }


            url = $"http://{settings.Host}/api/extensions/rust";
            if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Password))
            {
                return;
            }

            headers = new Dictionary<string, string>();
            string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
            headers["Authorization"] = $"Basic {token}";

            Puts("NetHawk extension loaded..");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Settings>();
            }
            catch (Exception ex)
            {
                Puts("Failed to load config, using default");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            settings = new Settings {
                Host = "127.0.0.1:5555",
                Username = "",
                Password = "",
                Whitelist = new List<string>(),
                ClientCommandFilters = new List<string>(),
            };

            Config.Save();
        }

        void sendToNetHawk(string ev, string ip)
        {
            sendToNetHawk(ev, ip, null);
        }

        void sendToNetHawk(string ev, string ip, string? message)
        {
            if (settings.Whitelist.Contains(ip))
            {
                return;
            }
            string payload = "";

            if (message == null)
            {
                payload = $"{ev}\n{serverName}\n{ip}";
            }
            else
            {
                payload = $"{ev}\n{serverName}\n{ip}\n{message}";
            }
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200)
                {
                    Puts($"[NetHawk] webrequest.Enqueue callback. host={Config["host"]} code={code}, response={response}");
                }
            }, this, RequestMethod.POST, headers, 5);
        }

        void OnRconCommand(IPAddress ip, string command, string[] args)
        {
            sendToNetHawk("OnRconCommand", ip.ToString(), $"{command} {string.Join(" ", args)}");
        }

        void OnRconConnection(IPAddress ip)
        {
            sendToNetHawk("OnRconConnection", ip.ToString());
        }

        void OnPlayerConnected(BasePlayer player)
        {
            sendToNetHawk("OnPlayerConnected", player.net.connection.IPAddressWithoutPort(), $"{player.displayName}\n{player.UserIDString}");
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            sendToNetHawk("OnPlayerCommand", player.net.connection.IPAddressWithoutPort(), $"{player.displayName}\n{command} {string.Join(", ", args)}");
            return null;
        }

        object OnClientCommand(Network.Connection connection, string command)
        {
            foreach (var filter in settings.ClientCommandFilters)
            {
                if (command.StartsWith(filter))
                {
                    return null;
                }
            }
            sendToNetHawk("OnClientCommand", connection.IPAddressWithoutPort(), command);
            return null;
        }
    }
}
