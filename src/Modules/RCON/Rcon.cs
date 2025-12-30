using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using DiscordUtilitiesAPI;
using DiscordUtilitiesAPI.Builders;
using DiscordUtilitiesAPI.Events;
using DiscordUtilitiesAPI.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace RCON
{
    public class RCON : BasePlugin, IPluginConfig<DUConfig>
    {
        public override string ModuleName => "[Discord Utilities] RCON";
        public override string ModuleAuthor => "SourceFactory.eu";
        public override string ModuleVersion => "1.2";
        private IDiscordUtilitiesAPI? DiscordUtilities { get; set; }
        public DUConfig Config { get; set; } = new();
        private HashSet<ulong> _adminRoleIds = new();
        private List<string> _serverNames = new();
        private List<Commands.SlashCommandOptionChoices> _serverChoices = new();
        public void OnConfigParsed(DUConfig config)
        {
            Config = config;

            _adminRoleIds.Clear();
            if (!string.IsNullOrWhiteSpace(config.AdminRolesId))
            {
                var roles = config.AdminRolesId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var role in roles)
                {
                    if (ulong.TryParse(role, out var roleId))
                        _adminRoleIds.Add(roleId);
                }
            }

            _serverNames = config.ServerList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            _serverChoices = _serverNames.Select(name => new Commands.SlashCommandOptionChoices
            {
                Name = name,
                Value = name
            }).ToList();
            if (_serverNames.Count > 1)
            {
                _serverChoices.Add(new Commands.SlashCommandOptionChoices
                {
                    Name = "All",
                    Value = "All"
                });
            }
        }
        public override void OnAllPluginsLoaded(bool hotReload)
        {
            GetDiscordUtilitiesEventSender().DiscordUtilitiesEventHandlers += DiscordUtilitiesEventHandler;
            DiscordUtilities!.CheckVersion(ModuleName, ModuleVersion);
        }
        public override void Unload(bool hotReload)
        {
            GetDiscordUtilitiesEventSender().DiscordUtilitiesEventHandlers -= DiscordUtilitiesEventHandler;
        }

        private void DiscordUtilitiesEventHandler(object? _, IDiscordUtilitiesEvent @event)
        {
            switch (@event)
            {
                case BotLoaded:
                    OnBotLoaded();
                    break;
                case SlashCommandExecuted slashCommand:
                    OnSlashCommandExecuted(slashCommand.Command, slashCommand.User);
                    break;
                default:
                    break;
            }
        }
        private void OnSlashCommandExecuted(CommandData command, UserData user)
        {
            if (command.CommandName == Config.CommandName)
            {
                if (DiscordUtilities!.Debug())
                    DiscordUtilities.SendConsoleMessage($"Slash command '{command.CommandName}' has been successfully logged", MessageType.Debug);

                if (_adminRoleIds.Count > 0)
                {
                    bool hasPermission = user.RolesIds.Any(r => _adminRoleIds.Contains(r));
                    if (!hasPermission)
                    {
                        var failedConfig = Config.RconFailedEmbed;
                        var embed = DiscordUtilities!.GetEmbedBuilderFromConfig(failedConfig, null);
                        var failedContent = Config.RconReplyEmbed.Content;
                        DiscordUtilities!.SendRespondMessageToSlashCommand(command.InteractionId, failedContent, embed, null, Config.RconFailedEmbed.SilentResponse);
                        return;
                    }
                }

                var options = command.OptionsData;
                string[] data = new string[2];
                foreach (var option in options)
                {
                    if (option.Name.Equals(Config.ServerOptionName, StringComparison.OrdinalIgnoreCase))
                    {
                        data[1] = option.Value;
                    }
                    else if (option.Name.Equals(Config.CommandOptionName, StringComparison.OrdinalIgnoreCase))
                    {
                        data[0] = option.Value;
                    }
                }
                if (!string.Equals(data[1], Config.Server, StringComparison.OrdinalIgnoreCase))
                {
                    if (!data[1].Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DiscordUtilities!.Debug())
                            DiscordUtilities.SendConsoleMessage($"This server is not '{data[1]}'! (RCON)", MessageType.Debug);
                        return;
                    }
                }

                var replaceVariablesBuilder = new ReplaceVariables.Builder
                {
                    CustomVariables = new Dictionary<string, string>{
                            { "{COMMAND}", data[0] },
                            { "{SERVER}", data[1] },
                        },
                };
                var config = Config.RconReplyEmbed;
                var embedBuider = DiscordUtilities!.GetEmbedBuilderFromConfig(config, replaceVariablesBuilder);
                var content = DiscordUtilities.ReplaceVariables(Config.RconReplyEmbed.Content, replaceVariablesBuilder);

                Server.ExecuteCommand(data[0]);
                DiscordUtilities.SendRespondMessageToSlashCommand(command.InteractionId, content, embedBuider, null, Config.RconReplyEmbed.SilentResponse);

            }
        }

        private void OnBotLoaded()
        {
            var commandData = new Commands.SlashCommandData
            {
                Name = Config.CommandName,
                Description = Config.CommandDescription
            };

            var optionChoices = _serverChoices.Count > 0 ? new List<Commands.SlashCommandOptionChoices>(_serverChoices) : new List<Commands.SlashCommandOptionChoices>();

            var commandOptions = new List<Commands.SlashCommandOptionsData>
            {
                new Commands.SlashCommandOptionsData
                {
                    Name = Config.ServerOptionName,
                    Description =  Config.ServerOptionDescription,
                    Type = SlashCommandOptionsType.String,
                    Required = true,
                    Choices = optionChoices.Count() > 0 ? optionChoices : null
                },
                new Commands.SlashCommandOptionsData
                {
                    Name = Config.CommandOptionName,
                    Description =  Config.CommandOptionDescription,
                    Type = SlashCommandOptionsType.String,
                    Required = true
                }
            };

            var command = new Commands.Builder
            {
                commandData = commandData,
                commandOptions = commandOptions
            };

            if (DiscordUtilities != null)
                DiscordUtilities.RegisterNewSlashCommand(command);
        }

        private IDiscordUtilitiesAPI GetDiscordUtilitiesEventSender()
        {
            if (DiscordUtilities is not null)
            {
                return DiscordUtilities;
            }

            var DUApi = new PluginCapability<IDiscordUtilitiesAPI>("discord_utilities").Get();
            if (DUApi is null)
            {
                throw new Exception("Couldn't load Discord Utilities plugin");
            }

            DiscordUtilities = DUApi;
            return DUApi;
        }
    }
}
