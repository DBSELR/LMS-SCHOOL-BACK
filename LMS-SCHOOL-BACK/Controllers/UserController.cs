// File: Controllers/UserController.cs (ADO.NET Patched)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using LMS.DTOs;
using LMS.Models;
using Azure.Core;

namespace LMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public UserController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private Dictionary<string, object> ReadRow(SqlDataReader reader)
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                row[camel] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            return row;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_GetAllUsers", conn) { CommandType = CommandType.StoredProcedure };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                users.Add(ReadRow(reader));
            return Ok(users);
        }

        //[HttpGet]
        //public async Task<IActionResult> GetUserRoles()
        //{
        //    var users = new List<object>();
        //    using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    using var cmd = new SqlCommand("sp_getuserroles", conn) { CommandType = CommandType.StoredProcedure };
        //    await conn.OpenAsync();
        //    using var reader = await cmd.ExecuteReaderAsync();
        //    while (await reader.ReadAsync())
        //        users.Add(ReadRow(reader));
        //    return Ok(users);
        //}

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_GetUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Ok(ReadRow(reader)) : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] RegisterUserRequest dto)
        {
            try
            {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_CreateUser", conn) { CommandType = CommandType.StoredProcedure };

                var usernameParam = new SqlParameter("@Username", SqlDbType.VarChar, 7)
                {
                    Direction = ParameterDirection.Output
                };

                //cmd.Parameters.AddWithValue("@Username", dto.Username ?? string.Empty);
                //  cmd.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(dto.Password));
                cmd.Parameters.AddWithValue("@PasswordHash", "TEMP");
                cmd.Parameters.AddWithValue("@Role", dto.Role);
            cmd.Parameters.AddWithValue("@Email", dto.Email);
            cmd.Parameters.AddWithValue("@FirstName", dto.FirstName);
            cmd.Parameters.AddWithValue("@LastName", dto.LastName);
            cmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
            cmd.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);
            cmd.Parameters.AddWithValue("@Gender", dto.Gender);
            cmd.Parameters.AddWithValue("@Address", dto.Address);
                cmd.Parameters.Add(usernameParam);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                var generatedUsername = usernameParam.Value?.ToString();
                if (string.IsNullOrEmpty(generatedUsername))
                    return StatusCode(500, "Username generation failed.");

                // Step 2: Use username as password
                var rawPassword = generatedUsername;
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword);

                // Step 3: Update password
                using var updateCmd = new SqlCommand("UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username", conn);
                updateCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                updateCmd.Parameters.AddWithValue("@Username", generatedUsername);
                await updateCmd.ExecuteNonQueryAsync();

                // Step 4: Get newly created UserId
                using var userIdCmd = new SqlCommand("SELECT UserId FROM Users WHERE Username = @Username", conn);
                userIdCmd.Parameters.AddWithValue("@Username", generatedUsername);
                var userIdObj = await userIdCmd.ExecuteScalarAsync();
                if (userIdObj == null)
                    return StatusCode(500, "Failed to retrieve UserId.");
                int userId = Convert.ToInt32(userIdObj);

                return Ok(new
                {
                    Username = generatedUsername,
                    Password = rawPassword,
                    Message = "User registered successfully"
                });
               // return Ok(new { message = "User registered successfully." });
            }
            //catch (SqlException ex)
            //{
            //    // Handle common SQL constraint violations
            //    if (ex.Number == 2627 || ex.Number == 2601) // Unique constraint violation
            //    {
            //        return Conflict(new { error = "A user with the same username or email already exists." });
            //    }

            //    // Other SQL errors
            //    return StatusCode(500, new { error = $"SQL error {ex.Number}: {ex.Message}" });
            //}
            catch (Exception ex)
            {
                // General fallback error
                return StatusCode(500, new { error = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest dto)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_UpdateUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", id);
            cmd.Parameters.AddWithValue("@Role", dto.Role);
            cmd.Parameters.AddWithValue("@Status", dto.Status);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_DeleteUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", id);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }

        [HttpPost("AssignRole/{userId}")]
        [HttpPost("ReassignRole/{userId}")]
        public async Task<IActionResult> AssignRole(int userId, [FromBody] AssignRoleRequest dto)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_AssignRole", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Role", dto.Role);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = $"Role {dto.Role} assigned successfully" });
        }

        [HttpPost("VerifyFeePayment/{studentId}")]
        public async Task<IActionResult> VerifyFeePayment(int studentId)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_User_VerifyFeePayment", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@StudentId", studentId);
            await conn.OpenAsync();
            try
            {
                using var reader = await cmd.ExecuteReaderAsync();
                return await reader.ReadAsync()
                    ? Ok(new { message = reader.GetString(0) })
                    : BadRequest("Fee check failed.");
            }
            catch (SqlException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class AssignRoleRequest
        {
            public string Role { get; set; }
        }
    }
}
