using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;

// TODO: Could move to seperate project to formalize no bidirectional dependency to Core
namespace VCF.RoleMiddleware
{
	public class RolePermissionMiddleware : CommandMiddleware
	{
		public override bool CanExecute(CommandContext ctx, ChatCommandAttribute command, MethodInfo method)
		{
			if (ctx.User.IsAdmin) return true;
			var storage = ctx.Services.GetService<RoleRepository>();
			return storage.CanUserExecuteCommand(ctx.User.CharacterName.ToString(), command.Id); // TODO: Better ID for user
		}
	}


	public interface IRoleStorage
	{
		void SetCommandPermission(string command, HashSet<string> roleIds);

		void SetUserRoles(string userId, HashSet<string> roleIds);

		HashSet<string> GetCommandPermission(string command);

		HashSet<string> GetUserRoles(string userId);

		HashSet<string> Roles { get; }
	}

	public class RoleRepository
	{
		private IRoleStorage _storage;
		
		public RoleRepository(IRoleStorage storage)
		{
			_storage = storage;
		}
		
		// this extends the IRoleStorage with ~CRUD operations
		public void AddUserToRole(string user, string role)
		{
			var roles = _storage.GetUserRoles(user) ?? new();
			roles.Add(role);
			_storage.SetUserRoles(user, roles);
		}
		
		public void RemoveUserFromRole(string user, string role)
		{
			var roles = _storage.GetUserRoles(user) ?? new();
			roles.Remove(role);
			_storage.SetUserRoles(user, roles);
		}

		public void AddRoleToCommand(string command, string role)
		{
			var roles = _storage.GetCommandPermission(command) ?? new();
			roles.Add(role);
			_storage.SetCommandPermission(command, roles);
		}

		public void RemoveRoleFromCommand(string command, string role)
		{
			var roles = _storage.GetCommandPermission(command) ?? new();
			roles.Remove(role);
			_storage.SetCommandPermission(command, roles);
		}

		public HashSet<string> ListUserRoles(string user) => _storage.GetUserRoles(user);

		public HashSet<string> ListCommandRoles(string command) => _storage.GetCommandPermission(command);

		public HashSet<string> Roles => _storage.Roles;

		public bool CanUserExecuteCommand(string user, string command)
		{
			var roles = _storage.GetCommandPermission(command);
			if (roles == null) return false;
			var userRoles = _storage.GetUserRoles(user);
			if (userRoles == null) return false;
			return roles.Any(userRoles.Contains);
		}
	}

	public class MemoryRoleStorage : IRoleStorage
	{
		// user -> [roles]	that the user has
		private Dictionary<string, HashSet<string>> _userRoles = new();

		// commands -> [roles] that can run
		private Dictionary<string, HashSet<string>> _commandPermissions = new();

		public HashSet<string> Roles => new(); // just in memory

		public void SetCommandPermission(string command, HashSet<string> roleIds)
		{
			foreach (var role in roleIds)
			{
				Roles.Add(role);
			}
			
			_commandPermissions[command] = roleIds;
		}

		public void SetUserRoles(string userId, HashSet<string> roleIds)
		{
			_userRoles[userId] = roleIds;
		}

		public HashSet<string> GetCommandPermission(string command)
		{
			return _commandPermissions.GetValueOrDefault(command);
		}

		public HashSet<string> GetUserRoles(string userId)
		{
			return _userRoles.TryGetValue(userId, out var roles) ? roles : new();
		}
	}

	// Other comopnents of the middleware
	public class RoleCommands
	{
		public record struct Role(string Name);

		public record struct User(string Id);
		
		public record struct Command(string Id);


		public class RoleConverter : ChatCommandArgumentConverter<Role>
		{
			public override bool TryParse(CommandContext ctx, string input, out Role result)
			{
				var repo = ctx.Services.GetRequiredService<RoleRepository>();
				if (repo.Roles.Contains(input))
				{
					result = new Role(input);
					return true;
				}

				throw ctx.Error("Invalid role"); // no we shouldn't do this because it's a try parse you dummy
			}
		}

		// TODO: need to match method FIRST then do type parsing so that we can get the correct expected type of argument and let the converter do common responses like "does not exit"
		// currently we'll run through them and you'd get all the errors for methods you're not matching ultimately.		
		public class UserConverter : ChatCommandArgumentConverter<User>
		{
			public override bool TryParse(CommandContext ctx, string input, out User result)
			{
				result = new User(input); // IDK how we handle offline users
				return true;
			}
		}


		private RoleRepository _roleRepository = new(new MemoryRoleStorage());

		// Role Management commands
		// - Create a role
		// - Assign a command to a role
		// - Assign a user to a role
		// - Remove a role from a user
		// - Remove a role from a command
		// - List all roles
		// - List all commands for a role
		// - LIst all users for a role
		// - List roles for user
		[ChatCommand("create")]
		public void CreateRole(CommandContext ctx, string name)
		{
			_roleRepository.Roles.Add(name);
		}

		[ChatCommand("allow")]
		public void AllowCommand(CommandContext ctx, Role role, Command command)
		{
			_roleRepository.AddRoleToCommand(command.Id, role.Name);
		}

		[ChatCommand("deny")]
		public void DenyCommand(CommandContext ctx, Role role, Command command)
		{
			_roleRepository.RemoveRoleFromCommand(command.Id, role.Name);
		}

		[ChatCommand("assign")]
		public void AssignUserToRole(CommandContext ctx, User user, Role role)
		{
			_roleRepository.AddUserToRole(user.Id, role.Name);
		}

		[ChatCommand("unassign")]
		public void UnassignUserFromRole(CommandContext ctx, User user, Role role)
		{
			_roleRepository.RemoveUserFromRole(user.Id, role.Name);
		}


		[ChatCommand("list")]
		public void ListRoles(CommandContext ctx)
		{
			ctx.Reply("Roles: " + string.Join(", ", _roleRepository.Roles));
		}

		[ChatCommand("list user")]
		public void ListRoles(CommandContext ctx, User user)
		{
			ctx.Reply($"Roles: {string.Join(", ", _roleRepository.ListUserRoles(user.Id))}");
		}

		[ChatCommand("list command")]
		public void ListCommands(CommandContext ctx, Command command)
		{
			ctx.Reply($"Roles: {string.Join(", ", _roleRepository.ListCommandRoles(command.Id))}");
		}
	}
}
