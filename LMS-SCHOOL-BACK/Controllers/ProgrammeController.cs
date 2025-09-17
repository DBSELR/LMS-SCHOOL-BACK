﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LMS.Data;
using LMS.Models;
using System.Data;
using Microsoft.Extensions.Configuration;
using static ExaminationController;

namespace LMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProgrammeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProgrammeController> _logger;
        private readonly string _connection;

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

        public ProgrammeController(AppDbContext context, ILogger<ProgrammeController> logger)
        {
            _context = context;
            _logger = logger;
            _connection = _context.Database.GetConnectionString();
        }

        [HttpGet("All")]
        public async Task<ActionResult<IEnumerable<Programme>>> GetAllProgrammes()
        {
            var result = new List<Programme>();
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_GetAllProgrammes", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new Programme
                {
                    ProgrammeId = (int)reader["ProgrammeId"],
                    ProgrammeName = reader["ProgrammeName"].ToString(),
                    ProgrammeCode = reader["ProgrammeCode"].ToString(),
                    NumberOfSemesters = (int)reader["NumberOfSemesters"],
                    Fee = (decimal)reader["Fee"],
                    Installments = (int)reader["Installments"],
                    BatchName = reader["BatchName"].ToString(),
                    IsCertCourse = reader["IsCertCourse"] != DBNull.Value && Convert.ToBoolean(reader["IsCertCourse"]),
                    IsNoGrp = reader["IsNoGrp"] != DBNull.Value && Convert.ToBoolean(reader["IsNoGrp"]),
                    CreatedDate = (DateTime)reader["CreatedDate"],
                    UpdatedDate = (DateTime)reader["UpdatedDate"]
                });
            }
            return Ok(result);
        }

        [HttpGet("ProgrammesWithSemesters")]
        public async Task<IActionResult> GetProgrammesWithSemesters()
        {
            var result = new List<object>();
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_GetProgrammesWithSemesters", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    ProgrammeName = reader["ProgrammeName"].ToString(),
                    Semester = reader["Semester"].ToString(),
                    CourseId = Convert.ToInt32(reader["CourseId"]),
                    Name = reader["Name"].ToString(),
                    CourseCode = reader["CourseCode"].ToString(),
                    Credits = Convert.ToInt32(reader["Credits"]),
                    CourseDescription = reader["CourseDescription"].ToString()
                });
            }
            return Ok(result);
        }

        [HttpGet("WithCourses/{programmeCode}")]
        public async Task<IActionResult> GetProgrammeWithCourses(string programmeCode)
        {
            var courses = new List<object>();
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_GetProgrammeWithCourses", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ProgrammeCode", programmeCode);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                courses.Add(new
                {
                    CourseId = (int)reader["CourseId"],
                    Name = reader["Name"].ToString(),
                    CourseCode = reader["CourseCode"].ToString(),
                    Credits = (int)reader["Credits"],
                    CourseDescription = reader["CourseDescription"].ToString(),
                    Semester = reader["Semester"].ToString()
                });
            }
            return courses.Count == 0 ? NotFound("No courses found for this programme.") : Ok(new { programmeCode, courses });
        }

        [HttpGet("ByCode/{code}")]
        public async Task<IActionResult> GetProgrammeByCode(string code)
        {
            Programme programme = null;
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_GetProgrammeByCode", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ProgrammeCode", code);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                programme = new Programme
                {
                    ProgrammeId = (int)reader["ProgrammeId"],
                    ProgrammeName = reader["ProgrammeName"].ToString(),
                    ProgrammeCode = reader["ProgrammeCode"].ToString(),
                    NumberOfSemesters = (int)reader["NumberOfSemesters"],
                    Fee = (decimal)reader["Fee"],
                    BatchName = reader["BatchName"].ToString(),
                    CreatedDate = (DateTime)reader["CreatedDate"],
                    UpdatedDate = (DateTime)reader["UpdatedDate"]
                };
            }
            return programme == null ? NotFound("Programme not found.") : Ok(programme);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Programme>> GetProgramme(int id)
        {
            Programme programme = null;
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_GetProgramme", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ProgrammeId", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                programme = new Programme
                {
                    ProgrammeId = (int)reader["ProgrammeId"],
                    ProgrammeName = reader["ProgrammeName"].ToString(),
                    ProgrammeCode = reader["ProgrammeCode"].ToString(),
                    NumberOfSemesters = (int)reader["NumberOfSemesters"],
                    Fee = (decimal)reader["Fee"],
                    BatchName = reader["BatchName"].ToString(),
                    IsCertCourse = reader["IsCertCourse"] != DBNull.Value && Convert.ToBoolean(reader["IsCertCourse"]),
                    CreatedDate = (DateTime)reader["CreatedDate"],
                    UpdatedDate = (DateTime)reader["UpdatedDate"]
                };
            }
            return programme == null ? NotFound() : Ok(programme);
        }

        //[HttpPost]
        //public async Task<ActionResult<Programme>> CreateProgramme([FromBody] Programme programme)
        //{
        //    Programme created = null;

        //    using (var conn = new SqlConnection(_connection))
        //    {
        //        await conn.OpenAsync();

        //        using (var cmd = new SqlCommand("sp_Programme_CreateProgramme", conn))
        //        {
        //            cmd.CommandType = CommandType.StoredProcedure;
        //            cmd.Parameters.AddWithValue("@ProgrammeName", programme.ProgrammeName);
        //            cmd.Parameters.AddWithValue("@ProgrammeCode", programme.ProgrammeCode);
        //            cmd.Parameters.AddWithValue("@NumberOfSemesters", programme.NumberOfSemesters);
        //            cmd.Parameters.AddWithValue("@Fee", programme.Fee);
        //            cmd.Parameters.AddWithValue("@BatchName", programme.BatchName);

        //            var isCertParam = new SqlParameter("@IsCertCourse", SqlDbType.Bit)
        //            {
        //                Value = programme.IsCertCourse
        //            };
        //            var IsNoGrp = new SqlParameter("@isNoGrp", SqlDbType.Bit)
        //            {
        //                Value = programme.IsNoGrp
        //            };

        //            cmd.Parameters.Add(isCertParam);
        //            cmd.Parameters.Add(IsNoGrp);

        //            using (var reader = await cmd.ExecuteReaderAsync())
        //            {
        //                if (await reader.ReadAsync())
        //                {
        //                    created = new Programme
        //                    {
        //                        ProgrammeId = (int)reader["ProgrammeId"],
        //                        ProgrammeName = reader["ProgrammeName"].ToString(),
        //                        ProgrammeCode = reader["ProgrammeCode"].ToString(),
        //                        NumberOfSemesters = (int)reader["NumberOfSemesters"],
        //                        Fee = (decimal)reader["Fee"],
        //                        BatchName = reader["BatchName"].ToString(),
        //                        IsCertCourse = (bool)reader["IsCertCourse"],
        //                        IsNoGrp = (bool)reader["IsNoGrp"],
        //                        CreatedDate = (DateTime)reader["CreatedDate"],
        //                        UpdatedDate = (DateTime)reader["UpdatedDate"]
        //                    };
        //                }
        //            }
        //        }
        //    }

        //    return CreatedAtAction(nameof(GetProgramme), new { id = created?.ProgrammeId }, created);
        //}

        [HttpPost]
        public async Task<ActionResult<Programme>> CreateProgramme([FromBody] Programme programme)
        {
            if (programme == null)
                return BadRequest("Programme data is missing.");

            try
            {
                Programme created = null;

                using (var conn = new SqlConnection(_connection))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("sp_Programme_CreateProgramme", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@ProgrammeName", programme.ProgrammeName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ProgrammeCode", programme.ProgrammeCode ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NumberOfSemesters", programme.NumberOfSemesters);
                        cmd.Parameters.AddWithValue("@Fee", programme.Fee);
                        cmd.Parameters.AddWithValue("@Installments", programme.Installments);
                        cmd.Parameters.AddWithValue("@BatchName", programme.BatchName ?? string.Empty);

                        //cmd.Parameters.AddWithValue("@IsCertCourse", programme.IsCertCourse);
                        //cmd.Parameters.AddWithValue("@IsNoGrp", programme.IsNoGrp);

                        var isCertParam = new SqlParameter("@IsCertCourse", SqlDbType.Bit)
                        {
                            Value = programme.IsCertCourse
                    };
                    var IsNoGrp = new SqlParameter("@isNoGrp", SqlDbType.Bit)
                    {
                        Value = programme.IsNoGrp
                    };

                    cmd.Parameters.Add(isCertParam);
                    cmd.Parameters.Add(IsNoGrp);




                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                created = new Programme
                                {
                                    ProgrammeId = reader.GetInt32(reader.GetOrdinal("ProgrammeId")),
                                    ProgrammeName = reader["ProgrammeName"].ToString(),
                                    ProgrammeCode = reader["ProgrammeCode"].ToString(),
                                    NumberOfSemesters = reader.GetInt32(reader.GetOrdinal("NumberOfSemesters")),
                                    Fee = reader.GetDecimal(reader.GetOrdinal("Fee")),
                                    BatchName = reader["BatchName"].ToString(),
                                    IsCertCourse = reader.GetBoolean(reader.GetOrdinal("IsCertCourse")),
                                    IsNoGrp = reader.GetBoolean(reader.GetOrdinal("IsNoGrp")),
                                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                    UpdatedDate = reader.GetDateTime(reader.GetOrdinal("UpdatedDate"))
                                };
                            }
                        }
                    }
                }

                if (created == null)
                    return StatusCode(500, "Programme was not created.");

                //return CreatedAtAction(nameof(GetProgramme), new { id = created.ProgrammeId }, created);
                return Ok(created); // instead of CreatedAtAction(...)

            }
            catch (SqlException ex)
            {
                // Log error if needed
                return StatusCode(500, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log error if needed
                return StatusCode(500, $"Unexpected error: {ex.Message}");
            }
        }



        [HttpPut("Update/{id}")]
        public async Task<IActionResult> UpdateProgramme(int id, [FromBody] Programme updated)
        {
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_UpdateProgramme", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ProgrammeId", id);
            cmd.Parameters.AddWithValue("@ProgrammeName", updated.ProgrammeName);
            cmd.Parameters.AddWithValue("@ProgrammeCode", updated.ProgrammeCode);
            cmd.Parameters.AddWithValue("@NumberOfSemesters", updated.NumberOfSemesters);
            cmd.Parameters.AddWithValue("@Fee", updated.Fee);
            cmd.Parameters.AddWithValue("@Installments", updated.Installments);
            cmd.Parameters.AddWithValue("@BatchName", updated.BatchName);
            // cmd.Parameters.AddWithValue("@IsCertCourse", updated.IsCertCourse);
            var isCertCourseParam = new SqlParameter("@IsCertCourse", SqlDbType.Bit)
            {
                Value = updated.IsCertCourse
            };
            var IsNoGrpParam = new SqlParameter("@IsNoGrp", SqlDbType.Bit)
            {
                Value = updated.IsNoGrp
            };
            cmd.Parameters.Add(isCertCourseParam);
            cmd.Parameters.Add(IsNoGrpParam);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProgramme(int id)
        {
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_Programme_DeleteProgramme", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ProgrammeId", id);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }


        [HttpPost("insertBatches")]
        public async Task<IActionResult> UpsertBatches([FromBody] List<BatchesDto> batch)
        {
            if (batch == null || batch.Count == 0)
                return BadRequest(new { message = "No batch provided." });

            try
            {
                using (SqlConnection conn = new SqlConnection(_connection))
                {
                    await conn.OpenAsync();

                    foreach (var dto in batch)
                    {
                        using (SqlCommand cmd = new SqlCommand("Sp_UpsertBatches", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@Bid", dto.Bid);
                            cmd.Parameters.AddWithValue("@BatchName", dto.BatchName);
                            cmd.Parameters.AddWithValue("@StartDate", dto.StartDate);
                            cmd.Parameters.AddWithValue("@EndDate", dto.EndDate);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                return Ok(new { message = "Batch saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database error", error = ex.Message });
            }
        }


        [HttpGet("GetAllBatch")]
        public async Task<IActionResult> GetAllBatch()
        {
            var result = new List<object>();
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_GetAllBatches", conn) { CommandType = CommandType.StoredProcedure };

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(ReadRow(reader));

            return Ok(result);
        }

        [HttpGet("GetBatchById")]
        public async Task<IActionResult> GetBatchById(int Bid)
        {
            var result = new List<object>();
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_GetBatchById", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Bid", Bid);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(ReadRow(reader));

            return Ok(result);
        }

        [HttpDelete("DeleteBatchById")]
        public async Task<IActionResult> DeleteBatchById(int Bid)
        {
            using var conn = new SqlConnection(_connection);
            using var cmd = new SqlCommand("sp_DeleteBatchById", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Bid", Bid);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }

        public class BatchesDto
        {
            public int Bid { get; set; } = 0;
            public string BatchName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
    }
}

