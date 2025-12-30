
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Linq;

namespace DiscordUtilities
{
    public partial class DiscordUtilities
    {
        private Dictionary<string, Dictionary<string, DateTime>> GetTimedRoles()
        {
            try
            {
                string filePath = $"{ModuleDirectory}/TimedRoles.json";
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, DateTime>>>(json) ?? new Dictionary<string, Dictionary<string, DateTime>>();
                }
                return new Dictionary<string, Dictionary<string, DateTime>>();
            }
            catch (Exception ex)
            {
                Perform_SendConsoleMessage($"An error occurred while loading Timed Roles: '{ex.Message}'", ConsoleColor.Red);
                throw new Exception($"An error occurred while loading Timed Roles: {ex.Message}");
            }
        }

        public void SetupAddNewTimedRole(SocketGuildUser user, SocketRole role, DateTime endTime)
        {
            try
            {
                var timedRoles = GetTimedRoles();
                var userId = user.Id.ToString();
                if (timedRoles.ContainsKey(userId))
                {
                    if (timedRoles[userId].ContainsKey(role.Id.ToString()))
                        timedRoles[userId][role.Id.ToString()] = endTime;
                    else
                        timedRoles[userId].Add(role.Id.ToString(), endTime);
                }
                else
                {
                    timedRoles.Add(userId, new Dictionary<string, DateTime>() { { role.Id.ToString(), endTime } });
                }

                string filePath = $"{ModuleDirectory}/TimedRoles.json";
                string json = JsonConvert.SerializeObject(timedRoles, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Perform_SendConsoleMessage($"User '{user.DisplayName}' ({user.Id}) has been added to role '{role.Name}' ({role.Id}) (Ends: '{endTime}')", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Perform_SendConsoleMessage($"An error occurred while adding new Timed Role: '{ex.Message}'", ConsoleColor.Red);
                throw new Exception($"An error occurred while adding new Timed Role: {ex.Message}");
            }
        }

        public void UpdateTimedRolesFile(Dictionary<string, Dictionary<string, DateTime>> timedRoles)
        {
            try
            {
                string filePath = $"{ModuleDirectory}/TimedRoles.json";
                string json = JsonConvert.SerializeObject(timedRoles, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Perform_SendConsoleMessage($"An error occurred while updating Timed Roles: '{ex.Message}'", ConsoleColor.Red);
                throw new Exception($"An error occurred while updating Timed Roles: {ex.Message}");
            }
        }

        public void CheckExpiredTimedRoles()
        {
            var timedRoles = GetTimedRoles();
            bool updateFile = false;
            var now = DateTime.Now;
            var usersToRemove = new List<string>();
            foreach (var x in timedRoles.ToList())
            {
                var rolesToRemove = x.Value.Where(role => role.Value < now).Select(role => role.Key).ToList();
                if (rolesToRemove.Count == 0)
                    continue;

                updateFile = true;
                RemoveRolesFromUser(ulong.Parse(x.Key), rolesToRemove);

                foreach (var role in rolesToRemove)
                {
                    if (timedRoles[x.Key].ContainsKey(role))
                        timedRoles[x.Key].Remove(role);
                }

                if (timedRoles[x.Key].Count == 0)
                    usersToRemove.Add(x.Key);
            }

            if (usersToRemove.Count > 0)
            {
                foreach (var userId in usersToRemove)
                {
                    timedRoles.Remove(userId);
                }
            }

            if (updateFile)
                UpdateTimedRolesFile(timedRoles);
        }
    }
}
