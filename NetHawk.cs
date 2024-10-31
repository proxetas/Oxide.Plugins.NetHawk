using System;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NetHawk", "jitcoder", "0.0.1")]
    [Description("Integrates with NetHawk WAF & DDOS Rust extension, provides insights into Rust player activities to provide additional DDOS protections")]
    public class NetHawk : RustPlugin
    {
        private string? serverName;
        private string? url;
        private Dictionary<string, string>? headers;

        private void Init()
        {
            serverName = ConVar.Server.hostname;
            LoadDefaultConfig();
            Puts("NetHawk extension loaded.");
        }

        [ConsoleCommand("nethawk")]
        void Configure(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin)
            {
                return;
            }

            if (command != "config")
            {
                player.Message($"invalid command '{command}'");
                return;
            }

            if (args.Length == 0)
            {
                player.Message("invalid command format. please provide host (and optionally http auth username password");
                return;
            }

            Config["host"] = args[0];

            if (args.Length > 1)
            {
                Config["username"] = args[1];
            }

            if (args.Length > 2)
            {
                Config["password"] = args[2];
            }

            Config.Save();
            LoadDefaultConfig();
            player.Message("NetHawk config updated");
        }

        protected override void LoadDefaultConfig()
        {
            string username;
            string password;
            string host;
            Config["host"] = host = Config["host"] as string ?? "127.0.0.1:5555";
            Config["username"] = username = Config["username"] as string ?? "";
            Config["password"] = password = Config["password"] as string ?? "";
            Config.Save();
            url = $"http://{host}/api/extensions/rust";
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return;
            }

            headers = new Dictionary<string, string>();
            string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            headers["Authorization"] = $"Basic {token}";
        }

        void sendToNetHawk(string ev, string ip)
        {
            sendToNetHawk(ev, ip, null);
        }

        void sendToNetHawk(string ev, string ip, string? message)
        {
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
                Puts($"[NetHawk] webrequest.Enqueue callback. code={code}, response={response}");
            }, this, RequestMethod.POST, headers);
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
            string ip = connection.IPAddressWithoutPort();
            Puts($"[NETHAWK] > {ip} > {command}");
            return null;
        }
    }
}
