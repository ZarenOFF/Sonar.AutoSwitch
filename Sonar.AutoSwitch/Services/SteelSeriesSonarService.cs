using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sonar.AutoSwitch.Services.Win32;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Services
{
    public class SteelSeriesSonarService : ISteelSeriesSonarService
    {
        private readonly string _connectionString;
        private int? _lastWorkingPort;

        public SteelSeriesSonarService()
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"SteelSeries\GG\apps\sonar\db\database.db")
            }.ToString();
            LoggingService.LogDebug($"[SonarService] Sonar DB: {_connectionString}");
        }

        public static SteelSeriesSonarService Instance { get; } = new();

        public IEnumerable<SonarGamingConfiguration> AvailableGamingConfigurations =>
            GetGamingConfigurations().OrderBy(s => s.Name);

        public IEnumerable<SonarGamingConfiguration> GetGamingConfigurations()
        {
            // Получение всех профилей из SQLite Sonar
            using var sqliteConnection = new SqliteConnection(_connectionString);
            sqliteConnection.Open();

            using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
            sqliteCommand.CommandText = "select id, name, vad from configs where vad == 1";
            using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
            while (sqliteDataReader.Read())
            {
                string id = sqliteDataReader.GetString(0);
                string name = sqliteDataReader.GetString(1);
                //LoggingService.LogDebug($"[SonarService] Profile founded: {name} ({id})");
                yield return new SonarGamingConfiguration(id, name);
            }
        }

        public async Task ChangeSelectedGamingConfiguration(
            SonarGamingConfiguration sonarGamingConfiguration,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sonarGamingConfiguration.Id))
            {
                LoggingService.LogDebug("[SonarService] No profile selected or ID is empty");
                return;
            }

            LoggingService.LogDebug($"[SonarService] Attempting to switch profile to: {sonarGamingConfiguration.Name} ({sonarGamingConfiguration.Id})");

            Process[] processesByName = Process.GetProcessesByName("SteelSeriesSonar");
            if (processesByName.Length <= 0 || cancellationToken.IsCancellationRequested)
            {
                LoggingService.LogDebug("[SonarService] SteelSeriesSonar process not found or operation cancelled");
                return;
            }

            // Какие PID и порты доступны
            var allPorts = processesByName.SelectMany(p =>
            {
                LoggingService.LogDebug($"[SonarService] PID Sonar: {p.Id}");
                var ports = NetworkHelper.GetPortById(p.Id, false).ToList();
                LoggingService.LogDebug($"[SonarService] Ports for process {p.Id}: {string.Join(", ", ports)}");
                return ports;
            }).ToList();

            IEnumerable<int> potentialPorts = allPorts;
            if (_lastWorkingPort != null)
            {
                potentialPorts = (new[] { _lastWorkingPort.Value }).Concat(potentialPorts);
            }
            _lastWorkingPort = null;

            using var httpClient = new HttpClient();
            foreach (int potentialPort in potentialPorts.Distinct())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogDebug("[SonarService] Operation cancelled while enumerating ports");
                    return;
                }

                string url = $"http://localhost:{potentialPort}/configs/{sonarGamingConfiguration.Id}/select";
                LoggingService.LogDebug($"[SonarService] Sending PUT: {url}");

                try
                {
                    HttpResponseMessage? httpResponseMessage = await httpClient.PutAsync(
                        url,
                        new StringContent(""),
                        cancellationToken
                    ).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null);

                    if (httpResponseMessage == null)
                    {
                        LoggingService.LogDebug($"[SonarService] HttpResponse error: request did not complete");
                        continue;
                    }
                    LoggingService.LogDebug($"[SonarService] Код ответа HTTP: {httpResponseMessage.StatusCode}");

                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        LoggingService.LogDebug($"[SonarService] Profile successfully applied via port {potentialPort}!");
                        _lastWorkingPort = potentialPort;
                        NotificationHelper.Send(
                            "Sonar: Profile switched",
                            $"Profile Activated: {sonarGamingConfiguration.Name}"
                        );
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogDebug($"[SonarService] Exception when accessing the API {url}: {ex.Message}");
                }
            }

            // Запомнить, какой профиль активирован, для UI
            try
            {
                StateManager.Instance.GetOrLoadState<HomeViewModel>().ActiveProfile = sonarGamingConfiguration;
            }
            catch (Exception ex)
            {
                LoggingService.LogDebug($"[SonarService] Error updating active profile: {ex.Message}");
            }
        }

        public string GetSelectedGamingConfiguration()
        {
            // Узнать какой профиль сейчас выбран в базе Sonar
            using var sqliteConnection = new SqliteConnection(_connectionString);
            sqliteConnection.Open();

            using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
            sqliteCommand.CommandText = "select config_id, vad from selected_config where vad == 1";
            using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
            if (!sqliteDataReader.Read())
            {
                LoggingService.LogDebug("[SonarService] Failed to get current profile");
                throw new InvalidOperationException("Unable to check for selected gaming profile");
            }
            var result = sqliteDataReader.GetString(0);
            LoggingService.LogDebug($"[SonarService] Active Sonar profile in database: {result}");
            return result;
        }
    }
}