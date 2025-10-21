using BepInEx.Unity.IL2CPP;
using ProjectM;
using ProjectM.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework.Breadstone;

namespace VampireCommandFramework.Common;

/// <summary>
/// Handles version checking against the Thunderstore API
/// </summary>
internal static class ThunderstoreVersionChecker
{
	private static readonly HttpClient _httpClient = new HttpClient();
	private const string THUNDERSTORE_API_BASE = "https://thunderstore.io/c/v-rising/api/v1/package/";
	
	static ThunderstoreVersionChecker()
	{
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "VampireCommandFramework-VersionChecker/1.0");
		_httpClient.Timeout = TimeSpan.FromSeconds(30);
	}

	static void SendMessageToClient(Entity userEntity, string message)
	{
		if (userEntity == default) return;

		// Queue ECS operations for main thread execution to avoid IL2CPP threading issues
		UnityMainThreadDispatcher.Enqueue(() =>
		{
			try
			{
				// Now we're on main thread - safe to access ECS components
				if (VWorld.Server?.EntityManager == null) return;
				if (!VWorld.Server.EntityManager.Exists(userEntity)) return;
				if (!VWorld.Server.EntityManager.HasComponent<User>(userEntity)) return;

				var user = VWorld.Server.EntityManager.GetComponentData<User>(userEntity);
				if (!user.IsConnected) return;

				var msg = new FixedString512Bytes(message);
				ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, user, ref msg);
			}
			catch (Exception ex)
			{
				Log.Debug($"Could not send message to client (user may have disconnected): {ex.Message}");
			}
		});
	}

	static void LogInfoAndSendMessageToClient(Entity userEntity, string message)
	{
		Log.Info(message);
		SendMessageToClient(userEntity, message);
	}

	static void LogWarningAndSendMessageToClient(Entity userEntity, string message)
	{

		Log.Warning(message);
		SendMessageToClient(userEntity, message.Color(Color.Gold));
	}

	/// <summary>
	/// Lists all installed plugins with their current versions (no update checking)
	/// </summary>
	public static void ListAllPluginVersions(Entity userEntity = default)
	{
		try
		{
			// Get all loaded plugins
			var installedPlugins = GetInstalledPlugins();
			
			if (installedPlugins.Count == 0)
			{
				LogInfoAndSendMessageToClient(userEntity, "No plugins found.");
				return;
			}

			LogInfoAndSendMessageToClient(userEntity, $"Installed Plugins ({installedPlugins.Count}):");
			
			// Sort plugins by name for easier reading
			foreach (var plugin in installedPlugins.OrderBy(p => p.Name))
			{
				var message = $"{plugin.Name.Color(Color.Command)}: {plugin.Version.Color(Color.Green)}";
				SendMessageToClient(userEntity, message);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Error listing plugin versions: {ex.Message}");
		}
	}

	/// <summary>
	/// Checks all installed plugins for newer versions on Thunderstore
	/// </summary>
	public static async Task CheckAllPluginVersionsAsync(Entity userEntity=default)
	{
		try
		{
			LogInfoAndSendMessageToClient(userEntity, "Starting plugin version check...");
			
			// Get all loaded plugins
			var installedPlugins = GetInstalledPlugins();
			LogInfoAndSendMessageToClient(userEntity, $"Found {installedPlugins.Count} installed plugins to check");

			// Get all packages from Thunderstore API for V Rising community
			var thunderstorePackages = await GetThunderstorePackagesAsync();
			if (thunderstorePackages == null || thunderstorePackages.Count == 0)
			{
				LogWarningAndSendMessageToClient(userEntity, "Could not retrieve Thunderstore package data");
				return;
			}

			Log.Info($"Retrieved {thunderstorePackages.Count} packages from Thunderstore");

			// Check each plugin against Thunderstore data
			var updatesFound = false;
			var resultMessage = new System.Text.StringBuilder();
			resultMessage.AppendLine($"Version check completed for {installedPlugins.Count} plugins:");
			
			foreach (var plugin in installedPlugins)
			{
				var updateInfo = CheckPluginForUpdate(plugin, thunderstorePackages);
				if (updateInfo != null)
				{
					updatesFound = true;
					AppendUpdateInfo(resultMessage, updateInfo);
					SendMessageToClient(userEntity, $"Update available for {plugin.Name.Color(Color.Command)}: {plugin.Version.Color(Color.Gold)} -> {updateInfo.LatestVersion.Color(Color.Green)}");
				}
			}

			if (!updatesFound)
			{
				resultMessage.AppendLine("* All plugins are up to date!");
				LogInfoAndSendMessageToClient(userEntity, "All installed plugins are up to date!");
			}
			else
			{
				resultMessage.AppendLine("! Updates are available for the plugins listed above.");
				resultMessage.AppendLine("* Note: Updates must be installed manually - VCF only identifies available updates.");
			}

			// Log the complete results as a single message
			Log.Info(resultMessage.ToString());
		}
		catch (Exception ex)
		{
			Log.Error($"Error during version check: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets information about all installed BepInEx plugins
	/// </summary>
	private static List<InstalledPluginInfo> GetInstalledPlugins()
	{
		var plugins = new List<InstalledPluginInfo>();

		foreach (var pluginKvp in IL2CPPChainloader.Instance.Plugins)
		{
			var pluginInfo = pluginKvp.Value;
			if (pluginInfo?.Metadata != null)
			{
				plugins.Add(new InstalledPluginInfo
				{
					GUID = pluginInfo.Metadata.GUID,
					Name = pluginInfo.Metadata.Name,
					Version = pluginInfo.Metadata.Version.ToString()
				});
			}
		}

		return plugins;
	}

	/// <summary>
	/// Retrieves all V Rising packages from Thunderstore API
	/// </summary>
	private static async Task<List<ThunderstorePackage>> GetThunderstorePackagesAsync()
	{
		try
		{
			var response = await _httpClient.GetAsync(THUNDERSTORE_API_BASE);
			if (!response.IsSuccessStatusCode)
			{
				Log.Warning($"Failed to retrieve Thunderstore data: HTTP {response.StatusCode}");
				return null;
			}

			var jsonContent = await response.Content.ReadAsStringAsync();
			var packages = JsonSerializer.Deserialize<List<ThunderstorePackage>>(jsonContent, new JsonSerializerOptions
			{
			PropertyNameCaseInsensitive = true
			});

			// All packages from the V Rising API endpoint are V Rising packages
			return packages ?? new List<ThunderstorePackage>();
		}
		catch (Exception ex)
		{
			Log.Error($"Error retrieving Thunderstore packages: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Checks if a plugin has an update available on Thunderstore
	/// </summary>
	private static PluginUpdateInfo CheckPluginForUpdate(InstalledPluginInfo plugin, List<ThunderstorePackage> thunderstorePackages)
	{
		try
		{
			// Try to find matching package by GUID first, then by name
			var matchingPackage = thunderstorePackages.FirstOrDefault(p => 
				p.Versions?.Any(v => v.Dependencies?.Any(d => d.Contains(plugin.GUID)) == true) == true)
				?? thunderstorePackages.FirstOrDefault(p => 
					string.Equals(p.Name, plugin.Name, StringComparison.OrdinalIgnoreCase));

			if (matchingPackage?.Versions == null || matchingPackage.Versions.Count == 0)
			{
				return null;
			}

			// Get the latest version
			var latestVersion = matchingPackage.Versions
				.OrderByDescending(v => DateTime.Parse(v.DateCreated))
				.FirstOrDefault();

			if (latestVersion == null)
			{
				return null;
			}

			// Compare versions
			if (IsNewerVersion(plugin.Version, latestVersion.VersionNumber))
			{
				return new PluginUpdateInfo
				{
					PluginName = plugin.Name,
					CurrentVersion = plugin.Version,
					LatestVersion = latestVersion.VersionNumber,
					ReleaseDate = latestVersion.DateCreated,
					PackageUrl = matchingPackage.PackageUrl
				};
			}

			return null;
		}
		catch (Exception ex)
		{
			Log.Debug($"Error checking update for {plugin.Name}: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Compares two version strings to determine if the second is newer
	/// </summary>
	private static bool IsNewerVersion(string currentVersion, string latestVersion)
	{
		try
		{
			var current = new Version(NormalizeVersion(currentVersion));
			var latest = new Version(NormalizeVersion(latestVersion));
			return latest > current;
		}
		catch
		{
			// If version parsing fails, do string comparison as fallback
			return string.Compare(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase) < 0;
		}
	}

	/// <summary>
	/// Normalizes version strings to ensure proper Version parsing
	/// </summary>
	private static string NormalizeVersion(string version)
	{
		if (string.IsNullOrWhiteSpace(version))
			return "0.0.0";

		// Remove any non-numeric prefixes (like 'v')
		version = version.TrimStart('v', 'V');

		// Ensure at least 3 parts (Major.Minor.Patch)
		var parts = version.Split('.');
		if (parts.Length == 1)
			return $"{parts[0]}.0.0";
		if (parts.Length == 2)
			return $"{parts[0]}.{parts[1]}.0";

		return version;
	}

	/// <summary>
	/// Appends information about available update to the StringBuilder
	/// </summary>
	private static void AppendUpdateInfo(System.Text.StringBuilder sb, PluginUpdateInfo updateInfo)
	{
		sb.AppendLine($">> UPDATE AVAILABLE: {updateInfo.PluginName}");
		sb.AppendLine($"   Current Version: {updateInfo.CurrentVersion}");
		sb.AppendLine($"   Latest Version: {updateInfo.LatestVersion}");
		sb.AppendLine($"   Release Date: {updateInfo.ReleaseDate}");
		if (!string.IsNullOrEmpty(updateInfo.PackageUrl))
		{
			sb.AppendLine($"   Thunderstore URL: {updateInfo.PackageUrl}");
		}
		sb.AppendLine(); // Add blank line between updates
	}

	/// <summary>
	/// Information about an installed plugin
	/// </summary>
	private class InstalledPluginInfo
	{
		public string GUID { get; set; }
		public string Name { get; set; }
		public string Version { get; set; }
	}

	/// <summary>
	/// Information about an available plugin update
	/// </summary>
	private class PluginUpdateInfo
	{
		public string PluginName { get; set; }
		public string CurrentVersion { get; set; }
		public string LatestVersion { get; set; }
		public string ReleaseDate { get; set; }
		public string PackageUrl { get; set; }
	}

	/// <summary>
	/// Thunderstore package data structure
	/// </summary>
	private class ThunderstorePackage
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("full_name")]
		public string FullName { get; set; }

		[JsonPropertyName("owner")]
		public string Owner { get; set; }

		[JsonPropertyName("package_url")]
		public string PackageUrl { get; set; }

		[JsonPropertyName("date_created")]
		public string DateCreated { get; set; }

		[JsonPropertyName("date_updated")]
		public string DateUpdated { get; set; }

		[JsonPropertyName("rating_score")]
		public int RatingScore { get; set; }

		[JsonPropertyName("is_pinned")]
		public bool IsPinned { get; set; }

		[JsonPropertyName("is_deprecated")]
		public bool IsDeprecated { get; set; }

		[JsonPropertyName("has_nsfw_content")]
		public bool HasNsfwContent { get; set; }

		[JsonPropertyName("categories")]
		public List<string> Categories { get; set; }

		[JsonPropertyName("communities")]
		public List<string> Communities { get; set; }

		[JsonPropertyName("versions")]
		public List<ThunderstoreVersion> Versions { get; set; }
	}

	/// <summary>
	/// Thunderstore package version data structure
	/// </summary>
	private class ThunderstoreVersion
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("full_name")]
		public string FullName { get; set; }

		[JsonPropertyName("description")]
		public string Description { get; set; }

		[JsonPropertyName("icon")]
		public string Icon { get; set; }

		[JsonPropertyName("version_number")]
		public string VersionNumber { get; set; }

		[JsonPropertyName("dependencies")]
		public List<string> Dependencies { get; set; }

		[JsonPropertyName("download_url")]
		public string DownloadUrl { get; set; }

		[JsonPropertyName("downloads")]
		public int Downloads { get; set; }

		[JsonPropertyName("date_created")]
		public string DateCreated { get; set; }

		[JsonPropertyName("website_url")]
		public string WebsiteUrl { get; set; }

		[JsonPropertyName("is_active")]
		public bool IsActive { get; set; }
	}
}
